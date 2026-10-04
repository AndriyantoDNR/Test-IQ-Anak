import { expect, Page, test } from '@playwright/test'
import { advanceAssessment, createChildAndStartAssessment as startAssessment } from './app-state'

async function createChildAndStartAssessment(page: Page) {
  await page.addInitScript(() => { (window as Window & { __E2E_MEMORY_DELAY__?: number }).__E2E_MEMORY_DELAY__ = 40 })
  await startAssessment(page, `WM Visual UAT ${Date.now()}`)
}

async function advanceUntilMemoryQuestion(page: Page) {
  await advanceAssessment(page, 'memory')
}

test('assessment naturally transitions from standard questions to visual sequence response', async ({ page }) => {
  await createChildAndStartAssessment(page)
  await advanceUntilMemoryQuestion(page)

  // Response controls must not be present while stimulus is still visible.
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeHidden()

  await expect(page.getByText('Susun ulang urutan')).toBeVisible({ timeout: 6000 })
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible()

  const responseOption = page.locator('.options button').first()
  await responseOption.click()
  await expect(page.getByText('Jawaban:')).toContainText('Jawaban:')

  await page.getByRole('button', { name: 'Undo' }).click()
  await page.getByRole('button', { name: 'Kirim' }).click()
  await expect(page.locator('.player')).toBeVisible()
})
