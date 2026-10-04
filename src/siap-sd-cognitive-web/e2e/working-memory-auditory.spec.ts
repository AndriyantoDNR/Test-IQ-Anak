import { expect, Page, test } from '@playwright/test'
import { createChildAndStartAssessment, currentAppState, submitCurrentAnswer, waitForNextAppState } from './app-state'

test.setTimeout(45_000)

async function installAuditoryHarness(page: Page) {
  await page.addInitScript(() => { (window as any).__E2E_MEMORY_DELAY__ = 40 })
  await page.addInitScript(() => {
    ;(window as any).__spoken = []
    ;(window as any).__cancelled = 0
    window.speechSynthesis.speak = ((utterance: SpeechSynthesisUtterance) => {
      ;(window as any).__spoken.push({ lang: utterance.lang, text: utterance.text })
      window.setTimeout(() => utterance.onstart?.(new Event('start') as SpeechSynthesisEvent), 0)
      window.setTimeout(() => utterance.onend?.(new Event('end') as SpeechSynthesisEvent), 20)
    }) as typeof window.speechSynthesis.speak
    window.speechSynthesis.cancel = (() => { (window as any).__cancelled++ }) as typeof window.speechSynthesis.cancel
  })
}

async function currentQuestionId(page: Page) {
  return (await currentAppState(page))?.questionId ?? null
}

async function waitForNextQuestion(page: Page, previousQuestionId: string) {
  await waitForNextAppState(page, previousQuestionId)
}

async function start(page: Page) {
  await installAuditoryHarness(page)
  await createChildAndStartAssessment(page, `WM Audio ${Date.now()}`)
}

async function advanceToAuditory(page: Page, title: string) {
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const auditory = page.getByTestId('memory-auditory')
    if (await auditory.isVisible().catch(() => false) && (await auditory.getAttribute('data-question-title'))?.startsWith(title)) return

    const questionId = await currentQuestionId(page)
    if (!questionId) {
      await expect.poll(() => currentQuestionId(page), { timeout: 7_000 }).not.toBeNull()
      continue
    }

    await test.step(`advance ${attempt + 1}: ${await page.locator('.player h2').first().textContent()}`, async () => {
      const state = await currentAppState(page)
      if (!state || state.state === 'complete' || state.state === 'parent-report' || state.state === 'detailed-report') throw new Error('No known assessment renderer is visible.')
      await submitCurrentAnswer(page, state.state)
      await waitForNextQuestion(page, questionId)
    })
  }
  throw new Error(`Auditory question "${title}" was not reached.`)
}

async function verifyAndSubmitAuditory(page: Page, title: string) {
  await test.step(`reach ${title}`, () => advanceToAuditory(page, title))
  await test.step('verify speech playback and locked controls', async () => {
    await expect(page.getByRole('button', { name: 'Kirim' })).toBeHidden()
    await expect.poll(() => page.evaluate(() => (window as any).__spoken as { lang: string; text: string }[]))
      .toEqual(expect.arrayContaining([expect.objectContaining({ lang: 'id-ID' })]))
  })
  await test.step('wait for responding phase and submit', async () => {
    await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7_000 })
    const previousQuestionId = await currentQuestionId(page)
    await page.locator('.options button').first().click()
    await page.getByRole('button', { name: 'Kirim' }).click()
    await waitForNextQuestion(page, previousQuestionId!)
  })
}

test('auditory word and instruction sequences speak id-ID and unlock after playback', async ({ page }) => {
  await start(page)
  await verifyAndSubmitAuditory(page, 'Word Sequence')
  await verifyAndSubmitAuditory(page, 'Instruction Sequence')
  await expect.poll(() => page.evaluate(() => (window as any).__cancelled as number)).toBeGreaterThan(0)
})
