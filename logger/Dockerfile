FROM ekidd/rust-musl-builder:stable as build
COPY logger/Cargo.* ./
RUN mkdir .cargo/ && cargo vendor > .cargo/config
COPY logger/ .
COPY proto/ ./src/hourai/db/proto/
RUN cargo build --release --verbose
RUN cargo install --path . --verbose

# Copy the statically-linked binary into a scratch container.
FROM alpine
RUN apk --update --no-cache add jsonnet
USER 969
ENV HOURAI_CONFIG=/opt/hourai.jsonnet
ENV HOURAI_ENV=dev
COPY --from=build /home/rust/.cargo/bin/hourai .
CMD ["jsonnet -m /config ${HOURAI_CONFIG} && /hourai"]
