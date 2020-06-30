import typing
from abc import ABC, abstractmethod
from hourai.db import proto


class Validator:

    def __init__(self):
        self.checks = []

    def add_check(self, check: ):
        self.checks.append(check)
        return self

    def validate(self, obj) -> typing.Iterator[str]:

    def validate_child(self, validator_cls: type, attr: str, *args, **kwargs) ->
                       typing.Iterator[str]:
        if hasattr(self.obj, attr):
            yield f"
        child_validator =

    @abstractmethod
    def run(self, obj: Any) -> typing.Iterator[str]:
        raise NotImplementedError()



class ActionValidator(Validator):

    def validate(self, obj: proto.Action) -> typing.Iterator[str]:
        try:
            validator = getattr(self, "_validate_" + obj.WhichOneOf("details"))
            yield from validator(obj)
        except AttributeError:
            yield f"No such action type: {obj.WhichOneOf('details')}"
