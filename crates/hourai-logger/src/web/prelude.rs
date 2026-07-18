use axum::{
    Json,
    http::StatusCode,
    response::{IntoResponse, Response},
};
use std::fmt::Display;

pub type Result<T> = core::result::Result<T, AppError>;

#[derive(Debug)]
pub struct AppError {
    pub status: StatusCode,
    pub message: String,
}

impl IntoResponse for AppError {
    fn into_response(self) -> Response {
        let body = Json(serde_json::json!({
            "status": self.status.as_u16(),
            "message": self.message,
        }));
        (self.status, body).into_response()
    }
}

pub fn http_error<T>(status: StatusCode, message: impl Display) -> Result<T> {
    Err(AppError {
        status,
        message: message.to_string(),
    })
}

#[allow(dead_code)]
pub fn http_internal_error<T>(message: impl Display) -> Result<T> {
    http_error(StatusCode::INTERNAL_SERVER_ERROR, message)
}

pub trait IntoHttpError<T> {
    fn http_error(self, status: StatusCode, message: impl Display) -> Result<T>;

    fn http_internal_error(self, message: impl Display) -> Result<T>
    where
        Self: std::marker::Sized,
    {
        self.http_error(StatusCode::INTERNAL_SERVER_ERROR, message)
    }
}

impl<T, E: std::fmt::Debug> IntoHttpError<T> for core::result::Result<T, E> {
    fn http_error(self, status: StatusCode, message: impl Display) -> Result<T> {
        match self {
            Ok(val) => Ok(val),
            Err(err) => {
                tracing::error!("http_error: {:?}", err);
                Err(AppError {
                    status,
                    message: message.to_string(),
                })
            }
        }
    }
}

impl<T> IntoHttpError<T> for Option<T> {
    fn http_error(self, status: StatusCode, message: impl Display) -> Result<T> {
        match self {
            Some(val) => Ok(val),
            None => Err(AppError {
                status,
                message: message.to_string(),
            }),
        }
    }
}
