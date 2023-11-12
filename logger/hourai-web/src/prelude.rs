use actix_web::error::ResponseError;
use actix_web::http::StatusCode;
use actix_web::{body::BoxBody, web::Json, HttpResponse};
use std::fmt::Display;
use thiserror::Error;

pub type Result<T> = core::result::Result<T, actix_web::Error>;
pub type JsonResult<T> = Result<Json<T>>;

pub fn http_error<T>(status_code: StatusCode, message: impl Display) -> Result<T> {
    Err(InternalError {
        message: message.to_string(),
        status_code,
    }
    .into())
}

pub trait IntoHttpError<T> {
    fn http_error(self, status_code: StatusCode, message: impl Display) -> Result<T>;

    fn http_internal_error(self, message: impl Display) -> Result<T>
    where
        Self: std::marker::Sized,
    {
        self.http_error(StatusCode::INTERNAL_SERVER_ERROR, message)
    }
}

impl<T, E: std::fmt::Debug> IntoHttpError<T> for core::result::Result<T, E> {
    fn http_error(
        self,
        status_code: StatusCode,
        message: impl Display,
    ) -> core::result::Result<T, actix_web::Error> {
        match self {
            Ok(val) => Ok(val),
            Err(err) => {
                tracing::error!("http_error: {:?}", err);
                Err(InternalError {
                    message: message.to_string(),
                    status_code,
                }
                .into())
            }
        }
    }
}

impl<T> IntoHttpError<T> for Option<T> {
    fn http_error(self, status_code: StatusCode, message: impl Display) -> Result<T> {
        match self {
            Some(val) => Ok(val),
            None => Err(InternalError {
                message: message.to_string(),
                status_code,
            }
            .into()),
        }
    }
}

#[derive(Error, Debug)]
#[error("{}", .message)]
pub struct InternalError {
    status_code: StatusCode,
    message: String,
}

impl InternalError {
    pub fn message(&self) -> String {
        if self.status_code.is_server_error() {
            self.status_code()
                .canonical_reason()
                .map(|r| r.to_owned())
                .unwrap_or_default()
        } else {
            self.to_string()
        }
    }
}

impl ResponseError for InternalError {
    fn error_response(&self) -> HttpResponse<BoxBody> {
        HttpResponse::build(self.status_code()).json(serde_json::json!({
            "status": self.status_code().as_u16(),
            "message": self.message()
        }))
    }

    fn status_code(&self) -> StatusCode {
        self.status_code
    }
}
