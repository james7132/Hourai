/// Forked version of tracing-actix-web, since it doesn't support the 4.0 betas yet.
use actix_web::dev::{Service, ServiceRequest, ServiceResponse, Transform};
use actix_web::Error;
use futures::future::{ok, Ready};
use futures::task::{Context, Poll};
use std::future::Future;
use std::pin::Pin;
use tracing::Span;
use tracing_futures::Instrument;
use uuid::Uuid;

pub struct TracingLogger;

impl<S, B> Transform<S, ServiceRequest> for TracingLogger
where
    S: Service<ServiceRequest, Response = ServiceResponse<B>, Error = Error>,
    S::Future: 'static,
    B: 'static,
{
    type Response = ServiceResponse<B>;
    type Error = Error;
    type Transform = TracingLoggerMiddleware<S>;
    type InitError = ();
    type Future = Ready<Result<Self::Transform, Self::InitError>>;

    fn new_transform(&self, service: S) -> Self::Future {
        ok(TracingLoggerMiddleware { service })
    }
}

#[doc(hidden)]
pub struct TracingLoggerMiddleware<S> {
    service: S,
}

impl<S, B> Service<ServiceRequest> for TracingLoggerMiddleware<S>
where
    S: Service<ServiceRequest, Response = ServiceResponse<B>, Error = Error>,
    S::Future: 'static,
    B: 'static,
{
    type Response = ServiceResponse<B>;
    type Error = Error;
    type Future = Pin<Box<dyn Future<Output = Result<Self::Response, Self::Error>>>>;

    fn poll_ready(&self, cx: &mut Context<'_>) -> Poll<Result<(), Self::Error>> {
        self.service.poll_ready(cx)
    }

    fn call(&self, req: ServiceRequest) -> Self::Future {
        let user_agent = req
            .headers()
            .get("User-Agent")
            .map(|h| h.to_str().unwrap_or(""))
            .unwrap_or("");
        let span = tracing::info_span!(
            "Request",
            request_path = %req.path(),
            user_agent = %user_agent,
            client_ip_address = %req.connection_info().realip_remote_addr().unwrap_or(""),
            request_id = %Uuid::new_v4(),
            status_code = tracing::field::Empty,
        );
        let fut = self.service.call(req);
        Box::pin(
            async move {
                let outcome = fut.await;
                let status_code = match &outcome {
                    Ok(response) => response.response().status(),
                    Err(error) => error.as_response_error().status_code(),
                };
                Span::current().record("status_code", &status_code.as_u16());
                outcome
            }
            .instrument(span),
        )
    }
}
