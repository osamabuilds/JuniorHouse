# 0004 — PostgreSQL transactional outbox at MVP, RabbitMQ later

- Status: Accepted
- Date: 2026-09-25

## Context
FR-61..65 and NFR-FT-01..08 require that critical notifications and domain events are never lost. The BRD mentions Azure Service Bus, RabbitMQ and a PostgreSQL outbox in different places.

## Decision
- Domain events and notifications are written to an `outbox` table in the same transaction as the business change.
- A background worker reads the table with `FOR UPDATE SKIP LOCKED`, dispatches each message, retries with exponential backoff (at least 5 attempts), and moves a message to a dead-letter status with an alert once retries run out.
- Consumers are idempotent, using an inbox/processed-message table.
- Move to RabbitMQ only when message volume needs it (BRD §13.8).

## Consequences
- No separate queue infrastructure at launch. The queue gets the same HA as the database.
- Throughput is limited by database load, which is acceptable at MVP volumes.
