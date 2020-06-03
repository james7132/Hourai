FROM node:14.3.0-alpine
WORKDIR /app
EXPOSE 8080
COPY package*.json /app/
RUN npm install
COPY . /app
CMD ["npm", "run", "serve"]
