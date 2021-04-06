FROM python:3.8-alpine as base

FROM base as builder
WORKDIR /app
RUN apk add --no-cache git protobuf gcc make libffi-dev build-base postgresql-dev
COPY base/requirements.txt .
RUN pip install --prefix /install -r requirements.txt
COPY base/ .
COPY proto/ .
RUN protoc $(find /app -type f -regex ".*\.proto") \
  --proto_path=/app \
  --python_out=/app

FROM base
WORKDIR /app
RUN addgroup -g 969 -S hourai && \
    adduser -u 969 -S hourai -G hourai && \
    apk add --no-cache libstdc++ postgresql-dev jsonnet && \
    mkdir /etc/hourai && \
    chown 969:969 /etc/hourai
USER 969

ENV HOURAI_CONFIG=/opt/hourai.jsonnet
ENV HOURAI_ENV=dev

COPY --from=builder /install /usr/local
COPY --from=builder /app /app
CMD /usr/bin/jsonnet -m /etc/hourai $HOURAI_CONFIG && python launcher.py run
