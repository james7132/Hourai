import _jsonnet
import json
import logging
import os
import time
import typing
from hourai.utils.tupperware import tupperware, conform, ProtectedDict


__DEFAULT = object()
__CONFIG = None
__LOADED_LISTS = {}


def load_config(file_path, env):
    global __CONFIG
    conf = _jsonnet.evaluate_file(file_path)
    config = json.loads(conf)
    config = config[env]
    conform(config, __make_configuration_template())
    __CONFIG = tupperware(config)
    __configure_logging(__CONFIG)
    logging.debug(json.dumps(json.loads(conf)[env], indent=2))
    return __CONFIG


def get_config():
    if __CONFIG is None:
        raise ConfigNotLoaded
    return __CONFIG


def load_list(config, name):
    global __LOADED_LISTS
    if name not in __LOADED_LISTS:
        directory = get_config_value(config, "list_directory",
                                     default="config/lists")
        path = os.path.join(directory, f"{name}.json")
        path = os.path.abspath(path)
        try:
            with open(path, 'r') as f:
                loaded_list = json.load(f)
                assert isinstance(loaded_list, typing.Iterable)
                __LOADED_LISTS[name] = tuple(loaded_list)
            logging.info(f'Loaded config list: {name} from {path}')
        except Exception:
            __LOADED_LISTS[name] = tuple()
            logging.exception(f'Config list could not be loaded from {path}. Using empty list.')
    return __LOADED_LISTS[name]


def get_config_value(config, path, *, type=__DEFAULT, default=__DEFAULT):
    value = config
    has_value = False
    try:
        for attr in path.split('.'):
            value = getattr(value, attr)
        has_value = True
    except AttributeError:
        pass
    if not has_value:
        if default != __DEFAULT:
            return default
        raise MissingConfigError(path, config)
    if type is not __DEFAULT and not isinstance(value, type):
        raise TypeError(f'Config value at "{path}" is not of type "{type}".')
    return value


def __configure_logging(conf):
    default_level = get_config_value(conf, 'logging.default', default=None)
    default_level = logging.getLevelName(default_level or 'WARNING')
    logging.basicConfig(
        level=default_level,
        format='%(asctime)s:%(levelname)s:%(module)s:%(message)s',
        datefmt='%Y-%m-%d %H:%M:%S')

    modules = get_config_value(conf, 'logging.modules', default=None)
    if modules is not None:
        for mod, level in modules._asdict().items():
            level = logging.getLevelName(level)
            logging.getLogger(mod).setLevel(level)

    # Log with UTC time
    for handler in logging.root.handlers:
        if handler.formatter is not None:
            handler.formatter.converter = time.gmtime


def __make_configuration_template():
    return {
        "command_prefix": "",

        "database": "",
        "redis": "",

        "activity": "",

        "disabled_extensions": [""],

        "list_directory": "",

        "web": {
            "port": 0
        },

        "music": {
            "nodes": [{
                "identifier": "",
                "host": "",
                "port": 0,
                "rest_uri": "",
                "region": "",
                "password": ""
            }]
        },

        "third_party": {
            "top_gg_token": ""
        },

        "discord": {
            "client_id": "",
            "client_secret": "",
            "redirect_uri": "",
            "bot_token": "",
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
            "access_log_format": "",
            "modules": ProtectedDict()
        },

        "webhooks": {
            "bot_log": "",
        },

        "private": ProtectedDict()
    }


class ConfigNotLoaded(Exception):
    pass


class MissingConfigError(Exception):

    def __init__(self, path, config):
        super().__init__(f'Missing config value under path "{path}". Full '
                         f'config: {str(config)}')
