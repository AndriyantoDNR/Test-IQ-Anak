import { expect, test } from '@playwright/test'
import { createChildAndStartAssessment, currentAppState, submitCurrentAnswer, waitForNextAppState } from './app-state'

test('child can start an assessment and submit a standard scalar answer', async ({ page }) => {
  await createChildAndStartAssessment(page, `Standard Browser UAT ${Date.now()}`)
  const current = await currentAppState(page)
  expect(current?.state).toBe('standard')
  await submitCurrentAnswer(page, 'standard')
  await expect.poll(() => currentAppState(page)).not.toEqual(current)
  await waitForNextAppState(page, current?.questionId, current?.state)
})
