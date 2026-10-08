# Cloud Credits Manager | Store listing - English (United States)

## Product name

Cloud Credits Manager

## Description

Cloud Credits Manager is a desktop application that shows how much Azure credit you have left, when it expires, and what your subscriptions are costing, directly from Windows.

It is made for people who receive Azure credits through programs such as sponsorships, Visual Studio subscriptions, or startup offers, and who want to keep an eye on them without opening a web portal.

Features include:

- Sign in with one or more Microsoft accounts, personal or work
- See every subscription your accounts can access, across directories
- Remaining credit, original amount, and expiration date for sponsorship credits
- Estimated remaining credit for Visual Studio monthly credits
- Cost this month, a projection to the end of the month, and six months of history
- Clear warnings for credit that is almost used up or about to expire
- Detects subscriptions that have no credit and are billed to your payment method while they still have resources
- Hide subscriptions that are billed to an organization
- Use a modern Windows desktop interface
- Authenticate securely through Microsoft identity services

Cloud Credits Manager only reads from Azure: it does not create, change, or delete anything in your subscriptions. It does not store cloud credentials. Authentication is handled through Microsoft identity services, and what you see depends on the permissions granted to the signed-in account.

The Visual Studio monthly credit has no public API, so the app estimates it from the monthly amount you enter and the cost of the current billing period.

## What's new in this version

Initial Microsoft Store submission.

This version includes sign-in with multiple accounts, subscription listing across directories, offer detection, sponsorship credit balance and credit lots, Visual Studio credit estimate, cost this month with projection and history, attention warnings, the organization subscriptions filter, and MSIX packaging.

## Category

- Primary: Productivity
- Secondary: Utilities & tools

## Privacy policy URL

https://ppiova.github.io/AzureCreditsApp/privacy-policy/

(Published with GitHub Pages from `docs/privacy-policy.md`: Settings > Pages > Source: "Deploy from a branch" > Branch: `main`, folder `/docs`.)

## Product features

- Sign in with several Microsoft accounts
- View subscriptions across directories
- Remaining Azure credit and expiration dates
- Visual Studio monthly credit estimate
- Cost this month with a projection
- Six months of cost history
- Warnings for credit running out or expiring
- Detects subscriptions billed to your payment method
- Secure authentication through Microsoft identity services

## Screenshots

Upload at least:

- `docs/store-assets/screenshot-1400x900.png` — credit and credit lots of a sponsorship

Additional screenshots:

- `docs/store-assets/screenshot-payg-1400x900.png` — a subscription billed to a payment method
- `docs/store-assets/screenshot-visual-studio-1400x900.png` — Visual Studio monthly credit estimate

The screenshots use sample data.

## Store logos

Generated assets:

- 9:16 Poster art: `docs/store-assets/poster-art-720x1080.png`
- 1:1 Box art: `docs/store-assets/box-art-1080x1080.png`
- 1:1 App tile icon: `docs/store-assets/app-tile-300x300.png`

## Short title

Cloud Credits Manager

## Voice title

Cloud Credits Manager

## Short description

Track your Azure credits from Windows. Sign in with your Microsoft accounts and see remaining credit, expiration dates, cost this month, and which subscriptions need attention.

## Keywords

- cloud credits
- cloud cost
- subscription credits
- sponsorship credits
- cost tracking
- cloud billing
- desktop tools

## Copyright and trademark info

Copyright (c) 2026 Pablito Piova. Microsoft and related service names are trademarks of Microsoft Corporation.

## Additional license terms

This application is licensed under the MIT License and is provided as-is, without warranty of any kind. Amounts shown are informational: credit balances and costs come from Azure and can lag behind the Azure portal, and Visual Studio credit is an estimate. Users remain responsible for the charges on their Azure subscriptions.

## Developed by

Pablito Piova

## Notes for certification

This app uses the `runFullTrust` capability because it is a packaged desktop application built with WPF. It uses full trust to run as a standard desktop application and to open the system browser for Microsoft sign-in. Cloud data is read through Microsoft identity services and the Azure Resource Manager APIs using the signed-in user's permissions. The app is read-only: it never creates, changes, or deletes cloud resources.

### How to test (for the certification reviewer)

The core features require signing in to a Microsoft account that has access to at least one Azure subscription.

1. Launch the app. Without an account it shows an empty dashboard with the message "No accounts yet".
2. Click **Add account**. Your default browser opens the Microsoft sign-in page; complete the sign-in and return to the app.
3. The app lists the subscriptions the account can access, with the offer, remaining credit (where the offer has credit), cost this month, and a status.
4. Select a subscription to see its details on the right: credit and credit lots, cost history, and subscription information. **Open in Azure portal** opens the subscription in the browser.
5. For a Visual Studio subscription, enter a monthly credit amount (for example `150`) and click **Save**; the app shows the estimated remaining credit.
6. **Hide not tracked** hides subscriptions billed to an organization. **Refresh** reloads everything.
7. The **✕** next to an account removes it from the app.

Reviewer access: please use a Microsoft account with an active Azure subscription. If a demo account is required, the developer can provide test credentials on request through Partner Center.

Notes:
- Credit is only shown for offers that have it (for example Azure Sponsorship). A Pay-As-You-Go subscription shows its cost and is marked "Billed to payment method"; that is expected.
- Cost data comes from Azure Cost Management, which limits request rates. Loading can take a little longer the first time; results are reused for up to three hours.
- A directory that requires additional sign-in (multi-factor authentication or conditional access) is skipped and reported in a yellow banner; the rest still loads.
- Without network access or a successful sign-in, the app shows a message and remains usable (it does not crash).
- The app stores only local data under its app data folder: which accounts are signed in (no passwords or tokens), monthly credit amounts entered by the user, display settings, and a short-lived cost cache. See the privacy policy for details.
