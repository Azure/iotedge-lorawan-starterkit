FROM node:10-alpine

WORKDIR /app/

COPY package*.json ./

RUN npm install --production

COPY app.js ./

USER node

CMD ["node", "app.js"]
