import { expect, Page } from '@playwright/test'

export type AppState = 'standard' | 'memory' | 'attention' | 'executive' | 'planning' | 'processing-speed' | 'complete' | 'parent-report' | 'detailed-report'

const selectors: Record<AppState, string> = {
  standard: '[data-testid="standard-question"]',
  // The player wrapper has no question ID; the active modality renderer does.
  memory: '[data-testid^="memory-"][data-question-id]',
  attention: '[data-testid^="attention-"][data-question-id]',
  executive: '[data-testid^="executive-"][data-question-id]',
  planning: '[data-testid="planning-player"]',
  'processing-speed': '[data-testid="processing-speed"]',
  complete: '[data-testid="assessment-complete"]',
  'parent-report': '[data-testid="parent-report"]',
  'detailed-report': '[data-testid="detailed-report"]'
}

export async function currentAppState(page: Page): Promise<{ state: AppState; questionId: string | null } | null> {
  for (const [state, selector] of Object.entries(selectors) as [AppState, string][]) {
    const locator = page.locator(selector).first()
    try {
      if (await locator.isVisible()) {
        return { state, questionId: await locator.getAttribute('data-question-id', { timeout: 0 }) }
      }
    } catch {
      // The renderer was replaced mid-read; treat it as a normal transition gap.
    }
  }
  return null
}

export async function waitForNextAppState(page: Page, previousQuestionId?: string | null, previousState?: AppState): Promise<{ state: AppState; questionId: string | null }> {
  let result: { state: AppState; questionId: string | null } | null = null
  await expect.poll(async () => {
    const current = await currentAppState(page)
    if (!current) return 'transitioning'
    if (current.state === 'complete' || current.state === 'parent-report' || current.state === 'detailed-report') { result = current; return current.state }
    if (previousState && current.state !== previousState) { result = current; return current.state }
    if (!previousQuestionId || current.questionId !== previousQuestionId) { result = current; return current.state }
    return 'transitioning'
  }, { timeout: 12_000, intervals: [50, 100, 250, 500] }).not.toBe('transitioning')
  if (!result) throw new Error('No valid application state reached.')
  return result
}

export async function createChildAndStartAssessment(page: Page, name = `Browser UAT ${Date.now()}`) {
  await page.goto('/')
  await page.getByLabel('Nama anak').fill(name)
  await page.getByRole('button', { name: 'Tambah anak' }).click()
  await expect(page.getByRole('combobox')).toHaveValue(/.+/)
  await page.getByRole('button', { name: 'Mulai' }).click()
  // Every new assessment starts in the standard renderer. Waiting on this exact
  // stable boundary avoids sampling the generic state scanner during React's
  // initial mount, while later progression continues to use currentAppState.
  await expect(page.getByTestId('standard-question')).toBeVisible({ timeout: 12_000 })
}

async function waitForMemoryResponseReady(page: Page) {
  const memory = page.getByTestId('memory-player')
  await expect(memory.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 12_000 })
  return memory
}

async function submitMemoryAnswer(page: Page) {
  const memory = await waitForMemoryResponseReady(page)
  const spatial = memory.getByTestId('spatial-response')
  if (await spatial.isVisible().catch(() => false)) await spatial.getByRole('button').first().click()
  else await memory.locator('.options button').first().click()
  await expect(memory.getByText('Jawaban:')).not.toContainText('—')
  await memory.getByRole('button', { name: 'Kirim' }).click()
}

async function submitAttentionAnswer(page: Page) {
  const section = page.locator('[data-testid^="attention-"]')
  const target = (await section.getByLabel('Target').textContent())?.replace(/^Target:\s*/, '').trim()
  const choices = section.locator('.memory-grid button')
  for (let index = 0; index < await choices.count(); index += 1) {
    if ((await choices.nth(index).textContent())?.trim() === target) await choices.nth(index).click()
  }
  await section.getByRole('button', { name: 'Kirim' }).click()
}

export async function submitCurrentAnswer(page: Page, state: AppState) {
  switch (state) {
    case 'standard':
      await page.getByTestId('standard-question').locator('.options button').nth(1).click()
      break
    case 'memory':
      await submitMemoryAnswer(page)
      break
    case 'attention':
      await submitAttentionAnswer(page)
      break
    case 'executive':
      await page.getByTestId('executive-player').locator('.options button').nth(1).click()
      await page.getByTestId('executive-player').getByRole('button', { name: 'Kirim' }).click()
      break
    case 'planning':
      for (const command of ['right', 'right', 'down', 'down']) await page.getByTestId(`command-${command}`).click()
      await page.getByTestId('submit').click()
      break
    case 'processing-speed':
      await page.getByTestId('processing-speed').locator('.options button').first().click()
      await page.getByTestId('processing-speed').getByRole('button', { name: 'Lanjut' }).click()
      break
    default:
      throw new Error(`Cannot submit an answer in ${state}.`)
  }
}

/** Advance through the assessment using each renderer's ordinary child-facing controls. */
export async function advanceAssessment(page: Page, until: AppState, observed = new Set<AppState>()) {
  for (let step = 0; step < 100; step += 1) {
    const current = await currentAppState(page)
    if (!current) {
      await expect.poll(() => currentAppState(page), { timeout: 12_000 }).not.toBeNull()
      continue
    }
    observed.add(current.state)
    if (current.state === until) return observed
    if (current.state === 'complete' || current.state === 'parent-report' || current.state === 'detailed-report') throw new Error(`Reached ${current.state} before ${until}.`)
    await submitCurrentAnswer(page, current.state)
    await waitForNextAppState(page, current.questionId, current.state)
  }
  throw new Error(`Did not reach ${until} within the assessment bounds.`)
}
