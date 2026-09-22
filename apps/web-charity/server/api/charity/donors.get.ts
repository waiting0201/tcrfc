// GET /api/charity/donors — 捐款徵信名單 `/{lang}/donors/`。
export default defineEventHandler(() => listNamedCompletedDonations())
