use twilight_command_parser::Arguments;
use std::str::FromStr;
use std::iter::Peekable;

trait ArgumentsExt {
    fn parse_next<T: FromArgument>(&mut self) ->
        Result<Option<T>, <T as FromArgument>::Err>;

    /// Parses until either the stream is done or the next argument cannot be parsed.
    fn parse_until<T: FromArgument>(&mut self) -> Vec<T> {
        let mut values = Vec::new();
        while let Ok(arg) = self.parse_next::<T>() {
            if let Some(value) = arg {
                values.push(value);
            } else {
                break;
            }
        }
        values
    }
}

trait FromArgument : Sized {
    type Err;
    fn parse_as(arg: impl AsRef<str>) -> Result<Self, Self::Err>;
}

impl ArgumentsExt for Peekable<Arguments<'_>> {

    fn parse_next<T: FromArgument>(&mut self) ->
        Result<Option<T>, <T as FromArgument>::Err> {
        if let Some(arg) = self.peek() {
            let result = T::parse_as(*arg)?;
            self.next();
            Ok(Some(result))
        } else {
            Ok(None)
        }
    }

}

impl<T: FromStr> FromArgument for T {
    type Err = <T as FromStr>::Err;
    fn parse_as(arg: impl AsRef<str>) -> Result<Self, Self::Err> {
        arg.as_ref().parse()
    }
}
