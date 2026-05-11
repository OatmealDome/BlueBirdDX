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

app.post("/api/tokenize", (req, res) => {
  const text = req.body.text;
  const entities = twitter.extractEntitiesWithIndices(text);
  
  const tokens = [];
  let currentIndex = 0;

  entities.forEach(entity => {
    const [start, end] = entity.indices;

    if (start > currentIndex) {
      tokens.push({
        type: 'Text',
        value: text.substring(currentIndex, start)
      });
    }

    let type = 'Unknown';
    if (entity.url) type = 'Url';
    else if (entity.hashtag) type = 'Hashtag';
    else if (entity.screenName) type = 'Mention';
    else if (entity.cashtag) type = 'Cashtag';

    tokens.push({
      type: type,
      value: text.substring(start, end)
    });

    currentIndex = end;
  });

  // Catch any remaining plain text at the end of the tweet
  if (currentIndex < text.length) {
    tokens.push({
      type: 'Text',
      value: text.substring(currentIndex)
    });
  }

  res.send(tokens);
});
