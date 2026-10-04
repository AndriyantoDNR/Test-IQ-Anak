import { expect, Page, test } from '@playwright/test'
import { advanceAssessment, createChildAndStartAssessment } from './app-state'

test.setTimeout(45_000)

async function start(page: Page) {
  await page.addInitScript(() => { (window as Window & { __E2E_MEMORY_DELAY__?: number }).__E2E_MEMORY_DELAY__ = 40 })
  await createChildAndStartAssessment(page, `WM Spatial ${Date.now()}`)
  await advanceAssessment(page, 'memory')
}

test('spatial memory locks grid during presentation then records ordered response', async ({ page }) => {
  await start(page)
  await expect(page.getByTestId('memory-sequence')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Kirim' })).toBeVisible({ timeout: 7000 })
  await page.getByRole('button', { name: 'Kirim' }).click()
  await expect(page.getByTestId('memory-spatial')).toBeVisible({ timeout: 7000 })
  await expect(page.getByTestId('spatial-response')).toBeHidden()
  await expect(page.getByTestId('spatial-response')).toBeVisible({ timeout: 7000 })
  await page.getByLabel('Kotak 0,0').click()
  await expect(page.getByText('Jawaban:')).toContainText('0,0')
  await page.getByRole('button', { name: 'Kirim' }).click()
})
