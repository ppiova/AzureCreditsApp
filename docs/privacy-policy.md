---
title: Privacy Policy
permalink: /privacy-policy/
---

# Privacy Policy — Cloud Credits Manager

_Last updated: 7 October 2026_

Cloud Credits Manager ("the app") is a desktop application developed by Pablito Piova that shows the remaining credit and recent cost of your Microsoft Azure subscriptions on Windows. This policy explains what the app accesses, what it stores, and what it does not do.

## Summary

- The app does **not** collect, transmit, or sell your personal data to the developer or any third party.
- The app has **no analytics, advertising, or tracking**.
- Authentication is handled by **Microsoft identity services**; the app never sees or stores your password.
- The app only **reads** from Azure. It does not create, change, or delete anything in your subscriptions.
- The only data the app stores is kept **locally on your own computer**.

## Authentication and Azure access

To read your subscriptions, the app signs you in interactively through **Microsoft Entra ID** using Microsoft's official identity libraries (MSAL). Sign-in happens in your web browser, on a Microsoft-hosted page. You can sign in with more than one account. The app:

- never receives, handles, or stores your account password;
- receives only the access tokens issued by Microsoft, which are managed by the Microsoft identity libraries and kept in their token cache on your computer, protected by Windows for your user account;
- performs every Azure request using **your own permissions** on your own subscriptions.

## What the app reads

While signed in, the app reads, from the Azure Resource Manager APIs, the information needed to show your credit and cost:

- the directories (tenants) and subscriptions your accounts can access: names, IDs, state, offer type, and spending limit;
- the billing accounts and billing profiles you can access, with their credit balance and credit lots (amounts and dates);
- the daily cost of each subscription for the last six months, and its current billing period;
- for subscriptions billed to a payment method, the list of resources, only to count them.

This information is shown to you in the app. It is **not** sent to the developer or any third party.

## What the app stores locally

The app stores a small amount of data **only on your computer**, under your user profile (`%AppData%\AzureCreditsApp`; when installed from the Microsoft Store, Windows keeps this folder inside the app's private storage):

- **Signed-in accounts**: for each account, its user name, account and directory identifiers, so you do not have to sign in every time. No passwords and no tokens are stored in these files.
- **Monthly credit amounts** you enter for subscriptions whose credit cannot be read from Azure (for example Visual Studio subscriptions), with the subscription ID they belong to.
- **Display settings**, such as whether organization subscriptions are hidden.
- **A cost cache**: the daily cost amounts of each subscription, by subscription ID, reused for up to three hours to avoid repeating slow requests.

These files never leave your machine. You can delete them at any time by removing that folder. Removing an account in the app deletes its record, and the app stops using that account; a sign-in token already in the Microsoft token cache simply expires. Uninstalling the Store version removes all of the app's data.

## What the app does not do

- It does not store your Azure or Microsoft account credentials.
- It does not change anything in your Azure subscriptions.
- It does not send telemetry, usage analytics, or crash reports anywhere.
- It does not share any data with the developer or third parties.
- It does not run background services; it only contacts Azure while it is open.

## Third-party services

The app communicates only with **Microsoft services** (Microsoft Entra ID for sign-in and Azure Resource Manager for subscription, billing, and cost data), under your account. Your use of those services is governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

## Changes to this policy

If this policy changes, the updated version will be published at this page with a new "Last updated" date.

## Contact

For questions about this policy, contact the developer at: **p.piova@gmail.com**.
