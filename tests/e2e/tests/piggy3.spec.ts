import { test, expect, Page } from '@playwright/test';

const USER = process.env.AUTH_USERNAME ?? 'admin';
const PASS = process.env.AUTH_PASSWORD ?? 'admin';
const SUFFIX = Date.now().toString(36);
let counter = 0;

test.beforeAll(() => {
  if (process.env.PIGGY3_E2E !== '1') {
    throw new Error(
      'Refusing to run: these tests delete all bookings and hide all accounts. ' +
        'Set PIGGY3_E2E=1 and point BASE_URL at a dedicated test stack (see README).',
    );
  }
});

function accountName(prefix: string): string {
  counter += 1;
  return `${prefix}-${SUFFIX}-${counter}`;
}

async function login(page: Page): Promise<void> {
  await page.goto('/Login');
  await page.fill('#Username', USER);
  await page.fill('#Password', PASS);
  await page.click('button[type="submit"]');
  await expect(page).not.toHaveURL(/\/Login/);
}

async function deleteAllBookings(page: Page): Promise<void> {
  await page.goto('/?tab=buchungen');
  for (;;) {
    const deleteButtons = page.getByRole('button', { name: 'Löschen', exact: true });
    if ((await deleteButtons.count()) === 0) break;
    await deleteButtons.first().click();
    await page.waitForLoadState('networkidle');
  }
}

async function deleteAllCosts(page: Page): Promise<void> {
  await page.goto('/?tab=kosten');
  for (;;) {
    const deleteButtons = page.getByRole('button', { name: 'Löschen', exact: true });
    if ((await deleteButtons.count()) === 0) break;
    await deleteButtons.first().click();
    await page.waitForLoadState('networkidle');
  }
  await page.goto('/?tab=kosten');
}

async function start(page: Page): Promise<void> {
  await login(page);
  await deleteAllBookings(page);
  await deleteAllCosts(page);
}

async function addCost(
  page: Page,
  name: string,
  type: string,
  value: string,
): Promise<void> {
  await page.goto('/?tab=kosten');
  await page.fill('#NewName', name);
  await page.fill('#NewValue', value);
  await page.selectOption('#NewType', type);
  await page.click('#AddMonthlyCost');
  await page.waitForLoadState('networkidle');
  await expect(page.locator(`input[name="name"][value="${name}"]`)).toBeVisible();
}

async function addBooking(page: Page, value: string, account: string): Promise<void> {
  await page.goto('/?tab=buchungen');
  await page.fill('#Value', value);
  await page.selectOption('#AccountId', { label: account });
  await page.getByRole('button', { name: 'Buchung hinzufügen' }).click();
  await page.waitForLoadState('networkidle');
}

async function accountId(page: Page, account: string): Promise<string> {
  return (await page.locator('#AccountId option', { hasText: account }).getAttribute('value'))!;
}

test('requires login and shows the three tabs', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/Login/);

  await login(page);

  await expect(page.getByRole('link', { name: 'Virtuelle Konten' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Monatliche Kosten' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Buchungen' })).toBeVisible();
});

test('tab 2 computes totals, edits, deletes and restores', async ({ page }) => {
  await start(page);
  const ausgabe = accountName('Ausgabe');
  const sparen = accountName('Sparen');

  await addCost(page, ausgabe, 'Ausgabe', '10,00');
  await addCost(page, sparen, 'Sparen', '20,00');

  await expect(page.locator('[data-type-total="Ausgabe"]')).toContainText('10,00 €');
  await expect(page.locator('[data-type-total="Sparen"]')).toContainText('20,00 €');
  await expect(page.locator('#monthly-grand-total')).toContainText('30,00 €');

  const ausgabeRow = page.locator('tr', {
    has: page.locator(`input[name="name"][value="${ausgabe}"]`),
  });
  await ausgabeRow.locator('input[name="value"]').fill('15,00');
  await ausgabeRow.getByRole('button', { name: 'Speichern' }).click();
  await page.waitForLoadState('networkidle');
  await expect(page.locator('#monthly-grand-total')).toContainText('35,00 €');

  await page.locator('tr', {
    has: page.locator(`input[name="name"][value="${sparen}"]`),
  }).getByRole('button', { name: 'Löschen' }).click();
  await page.waitForLoadState('networkidle');

  const deletedRow = page.locator('tr', {
    has: page.locator(`input[name="name"][value="${sparen}"]`),
  });
  await expect(deletedRow).toContainText('(gelöscht)');
  await expect(page.locator('#monthly-grand-total')).toContainText('15,00 €');

  await deletedRow.getByRole('button', { name: 'Wiederherstellen' }).click();
  await page.waitForLoadState('networkidle');
  await expect(page.locator('#monthly-grand-total')).toContainText('35,00 €');
});

test('tab 1 groups accounts and shows net sums', async ({ page }) => {
  await start(page);
  const ausgabe = accountName('Internet');
  const sparen = accountName('Urlaub');

  await addCost(page, ausgabe, 'Ausgabe', '10,00');
  await addCost(page, sparen, 'Sparen', '20,00');

  await addBooking(page, '-5,00', ausgabe);
  await addBooking(page, '2,00', sparen);

  await page.getByRole('link', { name: 'Virtuelle Konten' }).click();
  await page.waitForLoadState('networkidle');

  await expect(page.locator('[data-account-group="Ausgabe"]')).toBeVisible();
  await expect(page.locator('[data-account-group="Sparen"]')).toBeVisible();
  await expect(page.locator(`[data-account-row="${ausgabe}"]`)).toContainText('-5,00 €');
  await expect(page.locator(`[data-account-row="${sparen}"]`)).toContainText('2,00 €');
});

test('tab 3 guesses by absolute value, cycles focus and adds with Enter', async ({ page }) => {
  await start(page);
  const account = accountName('Internet');

  await addCost(page, account, 'Ausgabe', '53,80');

  await page.goto('/?tab=buchungen');
  await expect(page.locator('#Value')).toBeFocused();

  const accountId = await page
    .locator('#AccountId option', { hasText: account })
    .getAttribute('value');

  await page.fill('#Value', '53,80');
  await expect(page.locator('#AccountId')).toHaveValue(accountId!);

  await page.fill('#Value', '-53,80');
  await expect(page.locator('#AccountId')).toHaveValue(accountId!);

  await page.keyboard.press('Tab');
  await expect(page.locator('#AccountId')).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.locator('#Date')).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.locator('#Value')).toBeFocused();

  await page.keyboard.press('Enter');
  await page.waitForLoadState('networkidle');

  await expect(page.locator('#transaction-total')).toContainText('-53,80 €');
  const bookingRow = page.locator('table tbody tr').first();
  await expect(bookingRow).toContainText(account);
  await expect(bookingRow.locator('input[name="value"]')).toHaveValue('-53,80');
  await expect(page.locator('#Value')).toBeFocused();
});

test('tab 3 edits and deletes a booking in place', async ({ page }) => {
  await start(page);
  const account = accountName('Konto');

  await addCost(page, account, 'Ausgabe', '10,00');
  await addBooking(page, '-10,00', account);

  const row = page.locator('table tbody tr').first();
  await expect(row).toContainText(account);
  await row.locator('input[name="value"]').fill('12,34');
  await row.getByRole('button', { name: 'Speichern' }).click();
  await page.waitForLoadState('networkidle');

  await expect(page.locator('#transaction-total')).toContainText('12,34 €');

  await page
    .locator('table tbody tr')
    .first()
    .getByRole('button', { name: 'Löschen' })
    .click();
  await page.waitForLoadState('networkidle');

  await expect(page.getByText('Noch keine Buchungen.')).toBeVisible();
  await expect(page.locator('#transaction-total')).toContainText('0,00 €');
});

test('two equal monthly values do not guess', async ({ page }) => {
  await start(page);
  const first = accountName('Doppelt-A');
  const second = accountName('Doppelt-B');

  await addCost(page, first, 'Ausgabe', '53,80');
  await addCost(page, second, 'Ausgabe', '53,80');

  await page.goto('/?tab=buchungen');
  const secondId = await accountId(page, second);
  const firstId = await accountId(page, first);
  await page.selectOption('#AccountId', secondId);

  await page.fill('#Value', '-53,80');

  await expect(page.locator('#AccountId')).toHaveValue(secondId);
  await expect(page.locator('#AccountId')).not.toHaveValue(firstId);
});

test('soft-deleted account is hidden but its bookings stay in the total', async ({ page }) => {
  await start(page);
  const deleted = accountName('Alt');
  const other = accountName('Neu');

  await addCost(page, deleted, 'Ausgabe', '10,00');
  await addBooking(page, '-10,00', deleted);
  await addCost(page, other, 'Ausgabe', '20,00');

  await page.goto('/?tab=kosten');
  await page.locator('tr', {
    has: page.locator(`input[name="name"][value="${deleted}"]`),
  }).getByRole('button', { name: 'Löschen' }).click();
  await page.waitForLoadState('networkidle');

  await page.goto('/?tab=konten');
  await expect(page.locator(`[data-account-row="${deleted}"]`)).toHaveCount(0);
  await expect(page.locator(`[data-account-row="${other}"]`)).toBeVisible();

  await page.goto('/?tab=buchungen');
  await expect(page.locator('#AccountId option', { hasText: deleted })).toHaveCount(0);
  await expect(page.locator('#transaction-total')).toContainText('-10,00 €');
  await expect(page.locator('table tbody tr').first()).toContainText(`${deleted} (gelöscht)`);

  const otherId = await accountId(page, other);
  await page.selectOption('#AccountId', otherId);
  await page.fill('#Value', '10,00');
  await expect(page.locator('#AccountId')).toHaveValue(otherId);
});

test('books all monthly costs of the selected type', async ({ page }) => {
  await start(page);
  const first = accountName('Fixkosten');
  const second = accountName('Strom');
  const other = accountName('Sparen');

  await addCost(page, first, 'Ausgabe', '10,00');
  await addCost(page, second, 'Ausgabe', '20,00');
  await addCost(page, other, 'Sparen', '30,00');

  await page.goto('/?tab=buchungen');
  await page.selectOption('#BulkType', 'Ausgabe');
  await page.click('#BookMonthlyCosts');
  await page.waitForLoadState('networkidle');

  await expect(page.locator('table tbody tr')).toHaveCount(2);
  await expect(page.locator('#transaction-total')).toContainText('-30,00 €');
  const bookedAccounts = await page
    .locator('table tbody tr select[name="accountId"] option:checked')
    .allTextContents();
  expect(bookedAccounts.sort()).toEqual([first, second].sort());
  const bookedValues = await page
    .locator('table tbody tr input[name="value"]')
    .evaluateAll((elements) => elements.map((element) => (element as HTMLInputElement).value));
  expect(bookedValues.sort()).toEqual(['-20,00', '-10,00'].sort());
});

test('empty state renders without errors', async ({ page }) => {
  await start(page);

  await page.goto('/?tab=konten');
  await expect(page.getByText('Noch keine Konten.')).toBeVisible();

  await page.goto('/?tab=kosten');
  await expect(page.getByText('Noch keine monatlichen Kosten.')).toBeVisible();
  await expect(page.locator('#monthly-grand-total')).toContainText('0,00 €');

  await page.goto('/?tab=buchungen');
  await expect(page.getByText('Noch keine Buchungen.')).toBeVisible();
  await expect(page.locator('#transaction-total')).toContainText('0,00 €');
});
