import { expect, Page, test } from '@playwright/test'
import { advanceAssessment, createChildAndStartAssessment } from './app-state'

test.setTimeout(60_000)

async function start(page: Page) {
  await page.addInitScript(() => { (window as Window & { __E2E_MEMORY_DELAY__?: number }).__E2E_MEMORY_DELAY__ = 40 })
  await createChildAndStartAssessment(page, `WM Object ${Date.now()}`)
  await advanceAssessment(page, 'memory')
}
test('object sequence renders after the preceding working-memory modalities', async ({ page }) => {
  await start(page)
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.getByRole('button', { name: 'Kirim' }).click()
  await expect(page.getByTestId('memory-spatial')).toBeVisible({ timeout: 7000 })
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.getByRole('button', { name: 'Kirim' }).click()
  await expect(page.getByTestId('memory-sequence')).toBeVisible({ timeout: 7000 })
  await expect(page.getByTestId('memory-sequence')).toHaveAttribute('data-question-title', /^Object Sequence/)
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeHidden()
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.locator('.options button').first().click()
  await expect(page.getByText('Jawaban:')).not.toContainText('—')
  await page.getByRole('button', { name: 'Undo' }).click()
})
