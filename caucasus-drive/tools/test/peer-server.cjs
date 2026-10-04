// Локальный брокер PeerJS для online.mjs (IPv4, 127.0.0.1:9000): node peer-server.cjs &
const http = require('http');
const express = require('express');
const { ExpressPeerServer } = require('peer');
const app = express();
const server = http.createServer(app);
app.use('/', ExpressPeerServer(server, { path: '/', allow_discovery: false }));
server.listen(9000, '127.0.0.1', () => console.log('PeerJS broker: 127.0.0.1:9000'));
