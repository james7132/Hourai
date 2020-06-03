import logging


log = logging.getLogger(__name__)


def try_install():
    try:
        import uvloop
        uvloop.install()
        log.info('uvloop found and installed.')
    except ImportError:
        log.warn('uvloop not found, may not be running at peak '
                 'performance.')
