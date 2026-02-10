FROM node:18-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .
RUN npm run build && npm prune --omit=dev

EXPOSE 3000

CMD ["npm", "run", "start:prod"]
