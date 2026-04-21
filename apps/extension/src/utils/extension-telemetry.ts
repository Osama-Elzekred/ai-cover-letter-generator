import { getUserId } from './storage.js';
import { LoggingManager } from '../config/logging-config.js';

const API_BASE = 'http://localhost:5012/api/v1';

export type ExtensionEvent = {
  eventType: string;
  source: 'popup' | 'background' | 'content' | 'api-client';
  message?: string;
  level?: 'info' | 'warning' | 'error';
  success?: boolean;
  durationMs?: number;
  metadata?: Record<string, string>;
};

export type TelemetryMiddlewareContext = {
  metadata: Record<string, string>;
  successMessage?: string;
  failureMessage?: string;
  failureLevel?: 'warning' | 'error';
};

export type TelemetryMiddlewareOptions = {
  eventType: string;
  source: ExtensionEvent['source'];
  baseMetadata?: Record<string, string>;
  trackSuccess?: boolean;
  awaitDispatch?: boolean;
};

/**
 * Fire-and-forget extension event emission.
 * This should never throw to calling code.
 * 
 * Respects LoggingConfig: if logging is disabled, this is a no-op.
 */
export async function trackExtensionEvent(event: ExtensionEvent): Promise<void> {
  try {
    // Check if logging is enabled before dispatching
    const loggingEnabled = await LoggingManager.isEnabled();
    if (!loggingEnabled) {
      return; // Silent no-op
    }

    const userId = await getUserId();

    await fetch(`${API_BASE}/observability/extension/events`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-User-Id': userId
      },
      body: JSON.stringify({
        eventType: event.eventType,
        source: event.source,
        message: event.message,
        level: event.level ?? (event.success === false ? 'error' : 'info'),
        success: event.success ?? true,
        durationMs: event.durationMs,
        metadata: event.metadata
      })
    });
  } catch {
    // Intentionally swallow telemetry failures.
  }
}

/**
 * Middleware-style telemetry wrapper to keep call sites clean.
 */
export async function withTelemetry<T>(
  options: TelemetryMiddlewareOptions,
  operation: (context: TelemetryMiddlewareContext) => Promise<T>
): Promise<T> {
  const startedAt = Date.now();
  const context: TelemetryMiddlewareContext = {
    metadata: { ...(options.baseMetadata ?? {}) }
  };

  try {
    const result = await operation(context);

    if (options.trackSuccess !== false) {
      const successEvent: ExtensionEvent = {
        eventType: options.eventType,
        source: options.source,
        success: true,
        level: 'info',
        durationMs: Date.now() - startedAt,
        message: context.successMessage,
        metadata: context.metadata
      };

      if (options.awaitDispatch) {
        await trackExtensionEvent(successEvent);
      } else {
        void trackExtensionEvent(successEvent);
      }
    }

    return result;
  } catch (error: any) {
    const failureEvent: ExtensionEvent = {
      eventType: options.eventType,
      source: options.source,
      success: false,
      level: context.failureLevel ?? 'error',
      durationMs: Date.now() - startedAt,
      message: context.failureMessage ?? error?.message ?? `${options.eventType} failed`,
      metadata: context.metadata
    };

    if (options.awaitDispatch) {
      await trackExtensionEvent(failureEvent);
    } else {
      void trackExtensionEvent(failureEvent);
    }

    throw error;
  }
}
