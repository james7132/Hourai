FROM python:3.7-alpine as base

FROM base as builder
RUN apk add --no-cache \
  protobuf \
  gcc \
  make \
  libffi-dev \
  build-base \
  && mkdir /app
COPY requirements.txt /
RUN pip install --prefix /install -r /requirements.txt
COPY . /app
RUN protoc $(find /app -type f -regex ".*\.proto") \
  --proto_path=/app \
  --python_out=/app

FROM base
WORKDIR /app
RUN apk add --no-cache libstdc++
COPY --from=builder /install /usr/local
COPY --from=builder /app /app
CMD ["python", "launcher.py", "run"]
