# LaTeX Compiler — Queue + Worker Plan

## Summary
Keep local on-demand compilation for learning/dev. For publishing, implement a queue + worker pattern with Outbox/Inbox to improve security, scalability, reliability, and observability. This document outlines architecture, message contracts, DB schemas, implementation steps, operational controls, and verification steps.

## Goals
- Avoid spawning Docker containers from the API process.
- Run `latexmk` inside a dedicated, unprivileged worker container.
- Use an RDBMS-backed Outbox to guarantee reliable delivery and support transactional job creation.
- Implement Inbox deduplication on the worker to achieve at-least-once delivery safely.
- Provide async API (202 + jobId) and status endpoint to poll results.

## High-level Architecture
- API receives compile requests, creates a `CompileJob` row and an `OutboxMessage` row in a single DB transaction, then returns `202 Accepted` with `jobId`.
- Outbox dispatcher (background service) scans unsent outbox rows and publishes messages to RabbitMQ; marks messages sent atomically (or deletes them after success).
- Worker service consumes messages from RabbitMQ, performs compilation, stores PDF to configured storage (local volume or cloud), and updates `CompileJob` status.
- Worker records processed message IDs (Inbox table) to avoid duplicate work.

## Message contract (example)
```json
{
  "jobId": "guid",
  "userId": "string|null",
  "idempotencyKey": "string|null",
  "latex": "string",
  "options": {"target":"pdf", "timeoutSeconds":30}
}
```

## DB Schemas (Postgres examples)
- `compile_jobs`
  - ``id`` UUID PRIMARY KEY
  - `status` TEXT (Pending|Processing|Completed|Failed|Cancelled)
  - `user_id` TEXT NULL
  - `idempotency_key` TEXT NULL
  - `result_path` TEXT NULL
  - `error` TEXT NULL
  - `created_at`, `updated_at`

- `outbox_messages`
  - `id` BIGSERIAL PRIMARY KEY
  - `message_id` UUID UNIQUE
  - `topic` TEXT
  - `payload` JSONB
  - `attempts` INT DEFAULT 0
  - `dispatched_at` TIMESTAMP NULL
  - `created_at` TIMESTAMP

- `inbox_processed`
  - `message_id` UUID PRIMARY KEY
  - `processed_at` TIMESTAMP
  - `job_id` UUID NULL

## Outbox pattern notes
- API writes `compile_jobs` + `outbox_messages` in one transaction. This guarantees that if the API returns success, the outbox row exists and will be dispatched.
- An outbox dispatcher (background worker inside API process or separate service) publishes messages reliably and marks `dispatched_at` on success.
- Use broker-level dead-lettering plus application-level attempts/backoff.

## Inbox (worker) notes
- Worker consumer checks `inbox_processed` for `message_id`. If found, skip processing.
- After successful processing, insert row into `inbox_processed` and update `compile_jobs` within the same DB transaction used to record job result.

## Worker behavior
- Consumer receives message → mark job `Processing` → write LaTeX to temp file → run `latexmk` (timeout + capture logs) → on success upload PDF to storage and set job `Completed` with `result_path` → on failure set `Failed` with error.
- Enforce compile timeout (e.g. 30s default), file-size limits, and input validation.
- Run as unprivileged user, without host Docker socket mounted.

## Storage options
- Local: store PDFs in a mounted volume `/data/pdfs` accessible to API for downloads (dev/staging).
- Cloud: upload to S3 / Azure Blob and store signed URL in `result_path`.

## Dev environment (docker-compose) changes
- Add RabbitMQ service (management helpful for debugging):
```yaml
rabbitmq:
  image: rabbitmq:3-management
  container_name: coverletter_rabbitmq
  ports:
    - "5672:5672"
    - "15672:15672"
  networks:
    - coverletter_network
```
- Add `coverletter_worker` service built from `CoverLetter.Worker.LatexCompiler` Dockerfile. Worker image should include TeXlive or extend the `latexmk` image.

## API changes
- Replace direct `LatexCompilerService.CompileToPdfAsync()` calls with enqueue logic:
  - Create `CompileJob` DB row (status=Pending)
  - Insert `OutboxMessage` row (payload contains job data)
  - Return `202 Accepted` + `{ jobId, statusUrl }`
- Add `GET /api/v1/cv/compile/status/{jobId}` endpoint returning job status and `downloadUrl` when completed.

## Observability & Controls
- Metrics: compile duration histogram, success/failure counters, queue backlog gauge, outbox dispatch latency.
- Logs: include `jobId`, `messageId`, `userId`, `idempotencyKey`, and short failure reason.
- Rate-limiting: apply existing `ByokPolicy` and per-user quotas for compile requests.
- Resource limits: worker must be provisioned with limited CPU/memory (e.g., 1 CPU, 512MB) and concurrency caps.

## Retry & DLQ policy
- Configure broker to dead-letter messages after N failed deliveries (e.g., 5).
- Implement application-level retry/backoff using `attempts` in `outbox_messages` and `x-death` info from RabbitMQ.

## Security
- Do not mount host Docker socket inside API or worker.
- Run worker as non-root. Disable outbound network if possible.
- Validate and limit input length and complexity to avoid DoS via huge payloads.

## Testing & Verification
1. Unit tests for publisher, outbox dispatcher, and consumer logic.
2. Integration test: API enqueues job; worker processes and job completes; PDF retrievable.
3. Load test: simulate 10–50 concurrent compiles and observe queue/backlog and resource usage.

## Estimated effort
- Design and contract: 1 day
- Worker project + Dockerfile: 1–2 days
- API changes + DB migration: 1 day
- Observability + tests: 1–2 days
- Staging deploy and tuning: 1–2 days

## Next steps (suggested)
1. Add RabbitMQ to `docker-compose.dev.yml` and commit a migration for `compile_jobs` and `outbox_messages`.
2. Scaffold `CoverLetter.Worker.LatexCompiler` with a MassTransit consumer.
3. Implement outbox dispatcher (can be a hosted service inside API or separate small process).
4. Wire API to write job + outbox transactionally and add status endpoint.

---

Document created for learning and production readiness; ask if you want me to scaffold the worker and compose changes next.