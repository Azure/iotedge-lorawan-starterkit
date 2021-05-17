FROM arm64v8/node:10-slim

WORKDIR /app/

COPY package*.json ./

RUN npm install --production

COPY app.js ./

USER node

CMD ["node", "app.js"]
