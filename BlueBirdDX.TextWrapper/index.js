const express = require("express");
const twitter = require("twitter-text");

const app = express();
app.use(express.json());

const port = process.env.PORT || 3000;

app.listen(port, () => {
    console.log("Server now listening on " + port);
});

app.post("/api/count-characters", (req, res) => {
    const text = req.body.text;
    res.send({
        "length": twitter.getTweetLength(text)
    });
});

app.post("/api/extract-urls", (req, res) => {
    const text = req.body.text;
    res.send(twitter.extractUrlsWithIndices(text));
});

app.post("/api/extract-hashtags", (req, res) => {
    const text = req.body.text;
    res.send(twitter.extractHashtagsWithIndices(text));
});
