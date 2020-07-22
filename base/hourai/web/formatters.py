from abc import ABC, abstractmethod
from aiohttp import web
from google.protobuf import json_format, text_format


class ResponseFormatter(ABC):

    def __init__(self, status):
        self.status = status

    @abstractmethod
    def format_response(self, proto):
        raise NotImplementedError()


class JsonProtobufFormatter(ResponseFormatter):

    def format_response(self, proto):
        proto_json = json_format.MessageToDict(proto)
        return web.json_response(proto_json, status=self.status)


class BinaryProtobufFormatter(ResponseFormatter):

    MIME_TYPE = 'application/x-protobuf; messageType="{}"'

    def format_response(self, proto):
        content_type = self.MIME_TYPE.format(proto.DESCRIPTOR.name)
        return web.Response(body=proto.SerializeToString(),
                            content_type=content_type,
                            status=self.status)


class TextProtobufFormatter(ResponseFormatter):

    MIME_TYPE = 'application/x-protobuf-text; messageType="{}"'

    def format_response(self, proto):
        proto_text = text_format.MessageToString(proto)
        content_type = self.MIME_TYPE.format(proto.DESCRIPTOR.name)
        return web.Response(text=proto_text,
                            content_type=content_type,
                            status=self.status)
