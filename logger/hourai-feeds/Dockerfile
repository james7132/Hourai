FROM rust:alpine as build
ENV DOCKER_BUILD=1
WORKDIR /rust
RUN apk --update --no-cache add musl-dev
COPY logger/ .
COPY proto/ ./proto/
RUN cargo build --release --bins --verbose

# Copy the statically-linked binary into a scratch container.
FROM alpine
RUN apk --update --no-cache add jsonnet && \
    mkdir /etc/hourai && \
    chown 969:969 /etc/hourai
USER 969

ENV RUST_LOG=info
ENV HOURAI_CONFIG=/opt/hourai.jsonnet
ENV HOURAI_ENV=dev

COPY --from=build /rust/target/release/hourai-feeds .
CMD /usr/bin/jsonnet -m /etc/hourai $HOURAI_CONFIG && /hourai-feeds
