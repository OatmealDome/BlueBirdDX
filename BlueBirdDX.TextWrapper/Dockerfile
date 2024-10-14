FROM node:22

EXPOSE 80/tcp

ENV PORT=80

WORKDIR /app

COPY package.json package-lock.json /app
COPY index.js /app

RUN npm install

CMD ["node", "index.js"]
