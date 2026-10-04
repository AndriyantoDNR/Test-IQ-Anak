import { expect, Page, test } from '@playwright/test'
import { advanceAssessment, createChildAndStartAssessment } from './app-state'

test.setTimeout(60_000)

async function start(page: Page) {
  await page.addInitScript(() => { (window as Window & { __E2E_MEMORY_DELAY__?: number }).__E2E_MEMORY_DELAY__ = 40 })
  await createChildAndStartAssessment(page, `WM Object ${Date.now()}`)
  await advanceAssessment(page, 'memory')
}
async function failCurrentTrial(page: Page) {
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.getByRole('button', { name: 'Kirim' }).click()
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeHidden({ timeout: 3000 })
}
test('object sequence appears after completed visual and spatial ceilings', async ({ page }) => {
  await start(page)
  await failCurrentTrial(page); await failCurrentTrial(page)
  await expect(page.getByTestId('memory-spatial')).toBeVisible({ timeout: 7000 })
  await failCurrentTrial(page); await failCurrentTrial(page)
  await expect(page.getByTestId('memory-sequence')).toBeVisible({ timeout: 7000 })
  await expect(page.locator('[data-question-title="Object Sequence"]')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeHidden()
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.locator('.options button').first().click()
  await expect(page.getByText('Jawaban:')).not.toContainText('—')
  await page.getByRole('button', { name: 'Undo' }).click()
})
