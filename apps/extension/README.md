# Chrome Extension - AI Cover Letter Generator

**Phase 2**: Chrome extension that extracts job descriptions (LinkedIn, Wazaff, Indeed) and generates cover letters via backend API.

## Architecture

```
Extension (REST) → API Gateway (localhost:5012)
                      ↓ (gRPC - Phase 4+)
                   Microservices
```

**Current**: Monolithic API (Phase 1 ✅) | **Future**: Microservices with gRPC internal communication

## Backend API (`localhost:5012`)

**Required Headers:**
```http
X-User-Id: <uuid>
Idempotency-Key: <uuid>  # POST only, prevents duplicates
```

**Endpoints:**
- `POST /api/v1/cv/parse` - Upload CV (multipart/form-data) → returns `cvId`
- `POST /api/v1/cover-letters/generate` - Generate letter (requires `cvId`, `jobDescription`, `companyName`, `jobTitle`)
- `POST /api/v1/settings/groq-api-key` - Save user's API key (BYOK = unlimited requests)
- `GET /api/v1/settings/groq-api-key` - Check saved key
- `DELETE /api/v1/settings/groq-api-key` - Remove key

**Error Handling:**
- `200` → Success
- `400` → Validation error (show field errors)
- `404` → Not found (re-upload CV)
- `429` → Rate limited (prompt for API key)
- `500` → Server error (show traceId, allow retry)

**Retry Strategy:** Exponential backoff for network/5xx errors only. Don't retry 4xx.

## Extension Structure

```
apps/extension/
├── manifest.json
├── popup/           # Main UI
├── content/         # LinkedIn detector
├── background/      # Service worker (API calls)
├── utils/
│   ├── api-client.js
│   └── storage.js
└── assets/
```

## Implementation Checklist

**Step 1: Manifest** (`manifest.json` - Manifest V3, permissions: storage, activeTab)  
**Step 2: API Client** (`utils/api-client.js` - Handle all endpoints + errors)  
**Step 3: Storage** (`utils/storage.js` - Save CV, API key, preferences)  
**Step 4: Content** (`content/linkedin-detector.js` - Extract job data)  
**Step 5: Worker** (`background/service-worker.js` - Message handling)  
**Step 6: UI** (`popup/` - Upload CV, generate, display result)  
**Step 7: Test** (LinkedIn pages, rate limiting, BYOK bypass)

## Key Notes

- **User ID**: Generate UUID once, store in Chrome Storage, reuse
- **Idempotency**: Use `crypto.randomUUID()` per request, store with request for retries
- **CORS**: Already enabled for `chrome-extension://` origins
- **Rate Limit**: 10/min without key, unlimited with saved key
- **No gRPC**: Extensions use REST only (gRPC is for backend microservices Phase 4+)

## Docs & References

- API Docs: http://localhost:5012/scalar/v1
- Main Repo: `../../README.md`, `../../PROJECT-ROADMAP.md`
- Backend: `../../src/CoverLetter.Api/`
- Chrome Extension: [Manifest V3 Docs](https://developer.chrome.com/docs/extensions/mv3/)
