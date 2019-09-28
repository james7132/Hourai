import logging
import _jsonnet
import json
from hourai.utils.tupperware import tupperware, conform, ProtectedDict


__DEFAULT = object()
__CONFIG = None


def load_config(file_path, env):
    config = _jsonnet.evaluate_from_file(file_path)
    config = json.loads(config)
    config = config[env]
    conform(config, __make_configuration_template())
    __CONFIG = tupperware(config)
    __configure_logging(__CONFIG)
    return __CONFIG


def get_config():
    if __CONFIG is None:
        raise ConfigNotLoaded
    return __CONFIG


def get_config_value(config, path, *, type=__DEFAULT, default=__DEFAULT):
    value = config
    has_value = False
    try:
        for attr in path.split('.'):
            value = getattr(value, attr)
    except AttributeError:
        pass
    if not has_value:
        if default != __DEFAULT:
            return default
        raise MissingConfigError(path)
    if type is not __DEFAULT and not isinstance(value, type):
        raise TypeError(f'Config value at "{path}" is not of type "{type}".')
    return value


def __configure_logging(conf):
    default_level = get_config_value(conf, 'logging.default', default=None)
    if default_level is not None:
        logging.basicConfig(level=getattr(logging, default_level))

    modules = get_config_value(conf, 'logging.modules', default=None)
    if modules is not None:
        for mod, level in modules._asdict().items():
            logging.getLogger(mod).setLevel(getattr(logging, level))


def __make_configuration_template():
    return {
        "bot_token": "",
        "command_prefix": "",

        "database": "",
        "redis": "",

        "lavalink": {
            "nodes": [{
                "identifier": "",
                "host": "",
                "port": 0,
                "rest_uri": "",
                "region": "",
                "password": ""
            }]
        },

        "reddit": {
            "user_agent": "",
            "client_id": "",
            "client_secret": "",
            "username": "",
            "password": "",
        },

        # Logging can be arbitrary
        "logging": {
            "default": "",
            "modules": ProtectedDict()
        },

        "private": ProtectedDict()
    }


class ConfigNotLoaded(Exception):
    pass


class MissingConfigError(Exception):

    def __init__(self, path):
        super().__init__(f'Missing config value under path "{path}"')
