use actix_web::{web, HttpResponse};
use actix_web::error::ResponseError;
use actix_web::http::StatusCode;
use thiserror::Error;

pub type WebResult<T> = Result<T, WebError>;
pub type JsonResult<T> = WebResult<web::Json<T>>;

#[derive(Error, Debug)]
pub enum WebError {
    #[error("Redis Error: {}", .0)]
    RedisError(#[from] redis::RedisError),
    #[error("SQL Error: {}", .0)]
    SqlError(#[from] sqlx::Error),
}

impl WebError {
    pub fn is_server_error(&self) -> bool{
        match self {
            _ => true
        }
    }

    pub fn message(&self) -> String {
        if self.is_server_error() {
            "Internal server error.".to_owned()
        } else {
            self.to_string()
        }
    }
}

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
           _ => StatusCode::INTERNAL_SERVER_ERROR
        }
    }
}
