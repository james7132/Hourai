FROM python:3.8-alpine as base

FROM base as builder
WORKDIR /app
RUN apk add --no-cache git protobuf gcc make libffi-dev build-base postgresql-dev
COPY base/requirements.txt .
RUN pip install --prefix /install -r requirements.txt
COPY base/ .
COPY proto/ ./hourai/db/proto
RUN protoc $(find /app -type f -regex ".*\.proto") \
  --proto_path=/app \
  --python_out=/app

FROM base
WORKDIR /app
RUN apk add --no-cache libstdc++ postgresql-dev
# Run as non-root user
RUN addgroup -g 969 -S hourai && \
    adduser -u 969 -S hourai -G hourai
USER hourai
COPY --from=builder /install /usr/local
COPY --from=builder /app /app
CMD ["python", "launcher.py", "run"]
