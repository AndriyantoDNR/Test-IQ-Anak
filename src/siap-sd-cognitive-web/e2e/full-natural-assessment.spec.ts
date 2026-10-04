import { expect, test } from '@playwright/test'
import { advanceAssessment, createChildAndStartAssessment, currentAppState } from './app-state'

test.setTimeout(90_000)

test('a child can complete the natural assessment and open both reports', async ({ page }) => {
  await page.addInitScript(() => {
    ;(window as Window & { __E2E_MEMORY_DELAY__?: number; __E2E_SPEED_DURATION__?: number }).__E2E_MEMORY_DELAY__ = 20
    ;(window as Window & { __E2E_SPEED_DURATION__?: number }).__E2E_SPEED_DURATION__ = 1
  })
  await createChildAndStartAssessment(page, `Natural Assessment ${Date.now()}`)

  const seen = await advanceAssessment(page, 'complete')
  for (const state of ['standard', 'memory', 'attention', 'executive', 'planning', 'processing-speed'] as const) {
    expect(seen).toContain(state)
  }

  await page.getByRole('button', { name: 'Laporan untuk Orang Tua' }).click()
  await expect(page.getByTestId('parent-report')).toBeVisible()
  await page.getByRole('button', { name: 'Kembali' }).click()
  await expect((await currentAppState(page))?.state).toBe('complete')

  await page.getByRole('button', { name: 'Laporan Rinci' }).click()
  await expect(page.getByTestId('detailed-report')).toBeVisible()
})
