FROM node:14.3.0-alpine as builder
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

FROM nginx:alpine as production-stage
COPY --from=builder /app/docs /var/www/docs
COPY --from=builder /app/dist /var/www/html
EXPOSE 80 443
CMD ["nginx", "-g", "daemon off;"]
