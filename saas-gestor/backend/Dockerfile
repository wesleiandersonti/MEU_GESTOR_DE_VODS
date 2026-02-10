FROM node:18-alpine

WORKDIR /app

RUN apk add --no-cache python3 make g++

ENV npm_config_build_from_source=true
ENV PYTHON=/usr/bin/python3

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build

EXPOSE 3000

CMD ["node", "dist/main.js"]
