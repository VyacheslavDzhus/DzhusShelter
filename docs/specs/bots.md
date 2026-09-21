# Bots

**Route:** `/bots` · **Status:** Mockup

## Overview

Dashboard for a Telegram bot used to relay information (per the page's tagline "Боти для передачі інформації"). Shows bot status, today's message count, and a manual "send test message" action.

## Data Model

- `BotMessageLog`: Id, BotName, Direction (Sent/Received), SentAt — message count for "today" is a count query over this table, not a stored counter
- `Bot`: Id, Name, Platform (Telegram for now), IsActive

## Integrations

- [Telegram Bot API](https://core.telegram.org/bots/api) — requires a bot token from BotFather, and a chat ID to send to
- Sending a message = `sendMessage` call to the Telegram Bot API; needs the bot token stored as a secret (see `deploy-dzhus-shelter` skill — never hardcoded)

## Persistence

TBD — see [README.md](README.md#cross-feature-open-decisions). Message log is a natural fit for either engine.

## UI

Keep the existing card: status badge (Active/Inactive from `Bot.IsActive`), live message count from `BotMessageLog`, and the "Send Test Message" button. Since sending is a real side-effecting action (an actual Telegram message goes out), add a confirmation step before the button fires — don't send on a single click with no feedback.

## Open Decisions

- Where the bot token and target chat ID come from (config vs a `Bot` table field) — likely config/secret for the token, table field for chat ID
- Whether "today's message count" includes both sent and received, or just one direction
- Persistence engine (see above)

## Out of Scope

- Multi-bot management UI beyond what's needed to show one bot's status
- Two-way chat/inbox view — this is a status + trigger panel, not a chat client
