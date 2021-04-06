use super::CommandError;
use anyhow::Result;
use std::iter::Peekable;
use std::str::FromStr;
use twilight_command_parser::Arguments;

pub type PeekableArguments<'a> = Peekable<Arguments<'a>>;

pub trait ArgumentsExt {
    fn parse_next<T: FromArgument>(&mut self) -> Result<T>;

    fn parse_next_opt<T: FromArgument>(&mut self) -> Option<T> {
        self.parse_next().ok()
    }

    /// Parses until either the stream is done or the next argument cannot be parsed.
    fn parse_until<T: FromArgument>(&mut self) -> Vec<T> {
        let mut values = Vec::new();
        while let Ok(value) = self.parse_next::<T>() {
            values.push(value);
        }
        values
    }
}

pub trait FromArgument: Sized {
    type Err: std::error::Error + Send + Sync + 'static;
    fn parse_as(arg: impl AsRef<str>) -> Result<Self, Self::Err>;
}

impl<'a, I, S> ArgumentsExt for I
where
    I: Iterator<Item = S>,
    S: AsRef<str>,
{
    fn parse_next<T: FromArgument>(&mut self) -> Result<T> {
        if let Some(arg) = self.next() {
            Ok(T::parse_as(arg.as_ref())?)
        } else {
            anyhow::bail!(CommandError::MissingArgument)
        }
    }
}

impl<T: FromStr> FromArgument for T
where
    <T as FromStr>::Err: std::error::Error + Send + Sync + 'static,
{
    type Err = <T as FromStr>::Err;
    fn parse_as(arg: impl AsRef<str>) -> Result<Self, Self::Err> {
        arg.as_ref().parse()
    }
}
