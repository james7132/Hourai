use actix_web::error::ResponseError;
use actix_web::http::StatusCode;
use actix_web::{web, HttpResponse};
use thiserror::Error;

pub type WebResult<T> = Result<T, WebError>;
pub type JsonResult<T> = WebResult<web::Json<T>>;

#[derive(Error, Debug)]
pub enum WebError {
    #[error("Generic response error: {}", .0)]
    ResponseError(Box<dyn ResponseError>),
    #[error("HTTP Error: {}", .0)]
    GenericHTTPError(StatusCode),
    #[error("Redis Error: {}", .0)]
    RedisError(#[from] redis::RedisError),
    #[error("SQL Error: {}", .0)]
    SqlError(#[from] sqlx::Error),
}

impl WebError {
    pub const UNAUTHORIZED: WebError = WebError::GenericHTTPError(StatusCode::UNAUTHORIZED);
    pub const NOT_FOUND: WebError = WebError::GenericHTTPError(StatusCode::UNAUTHORIZED);

    pub fn message(&self) -> String {
        if self.status_code().is_server_error() {
            self.status_code()
                .canonical_reason()
                .map(|r| r.to_owned())
                .unwrap_or_else(|| String::new())
        } else {
            self.to_string()
        }
    }
}

impl From<StatusCode> for WebError {
    fn from(value: StatusCode) -> Self {
        WebError::GenericHTTPError(value)
    }
}

macro_rules! box_error {
    ($type:ty) => {
        impl From<$type> for WebError {
            fn from(value: $type) -> Self {
                WebError::ResponseError(Box::new(value))
            }
        }
    };
}

box_error!(awc::error::JsonPayloadError);
box_error!(awc::error::PayloadError);
box_error!(awc::error::SendRequestError);

impl ResponseError for WebError {
    fn error_response(&self) -> HttpResponse {
        let status = self.status_code();
        HttpResponse::build(status)
            .header("Content-Type", "application/json")
            .json(serde_json::json!({
                "status": status.as_u16(),
                "message": self.message()
            }))
    }

    fn status_code(&self) -> StatusCode {
        match self {
            Self::ResponseError(err) => err.status_code(),
            Self::GenericHTTPError(code) => *code,
            _ => StatusCode::INTERNAL_SERVER_ERROR,
        }
    }
}
