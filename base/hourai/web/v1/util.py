from aiohttp import web
from google.protobuf import json_format


DEFAULT_MESSAGES = {
    403: "Forbidden: You are not authorized to access this resource.",
    500: "Unknown server error occured."
}


def error_response(error_code, message=None):
    return web.json_response({
        "status": error_code,
        "message": str(message) or DEFAULT_MESSAGES.get(error_code, None)
    }, status=error_code)


def protobuf_json_response(message):
    model_json = json_format.MessageToDict(message)
    return web.json_response(model_json, status=200)


def protobuf_json_request(request, model_type):
    try:
        model = model_type()
        json_format.ParseDict(request.json(), model)
        return model
    except json_format.Error:
        raise web.HTTPBadRequest()
