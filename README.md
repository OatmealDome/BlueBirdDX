# BlueBirdDX

Post to multiple social media sites simultaneously.

This application was built for [my own personal usage](https://twitter.com/OatmealDome). I can't guarantee that I'll be able to provide support if you choose to use it.

## Architecture

BlueBirdDX is made up of two primary applications:

* Core: runs in the background and sends posts to each social media site
* WebApp: allows the user to create threads and upload media via their web browser (also contains an API backend)

MongoDB is used as the database system, and media files are stored on an Amazon S3 bucket. If a post contains a quoted tweet, Selenium WebDriver is used to generate a screenshot of the tweet which is then attached to posts made on non-Twitter social media services. gRPC is used to call functions on the Core from the WebApp.

TextWrapper is a small Node application that wraps the twitter-text library. Please check [its README file](BlueBirdDX.TextWrapper/README.md) for more information.

BlueBirdDX expects to be run in a Docker environment, but it can be run outside of one for development purposes.

Please note that the WebApp has no built-in authentication or authorization! **If you expose the WebApp directly to the Internet, anyone can make posts using your accounts!**

## Setup

A sample set up with Docker is detailed below.

### Initial Steps

To generate the necessary applications and/or secrets for each social media site, please consult their documentation:

* [Twitter](https://developer.twitter.com)
* [Bluesky](https://bsky.app/settings/app-passwords) (while using your account's password does work, Bluesky recommends creating an app-specific password for each third-party application)
* [Mastodon](https://docs.joinmastodon.org/client/token/)
* [Threads](https://developers.facebook.com/docs/threads/get-started)

Here is a sample `docker-compose.yml` file which can be used to run the application (replace secrets management with something better in production!):

```yaml
services:
  mongo:
    image: mongo:6
    environment:
      MONGO_INITDB_ROOT_USERNAME: bluebirddx
      MONGO_INITDB_ROOT_PASSWORD: password

  selenium-standalone-chrome:
    image: selenium/standalone-chrome
    restart: unless-stopped

  textwrapper:
    image: ghcr.io/oatmealdome/bluebirddxtextwrapper
    restart: unless-stopped
    environment:
      PORT: 80

  core:
    image: ghcr.io/oatmealdome/bluebirddx
    restart: unless-stopped
    depends_on:
      - mongo
      - selenium-standalone-chrome
    volumes:
      - ./core-data:/data
    environment:
      Mongo__ConnectionString: "mongodb://bluebirddx:password@mongo:27017/"
      Mongo__Database: "bluebirddx"
      S3__ServiceUrl: "<S3 service URL>"
      S3__Bucket: "<S3 bucket name>"
      S3__AccessKey: "<S3 access key>"
      S3__SecretAccessKey: "<S3 access key secret>"
      PostThreadManager__TextWrapperServer: "http://textwrapper"
      PostThreadManager__SeleniumNodeUrl: "http://selenium-standalone-chrome:4444/wd/hub"
      PostThreadManager__WebAppUrl: "http://webapp:8080"
      # For Twitter support
      SocialApp__TwitterClientId: "<Twitter client ID>"
      SocialApp__TwitterClientSecret: "<Twitter client secret>"
      # For Threads support
      SocialApp__ThreadsAppId: 1234 # Threads app ID
      SocialApp__ThreadsAppSecret: "<Threads app secret>"

  webapp:
    image: ghcr.io/oatmealdome/bluebirddxwebapp
    restart: unless-stopped
    depends_on:
      - mongo
    ports:
      - 80:8080
    environment:
      Mongo__ConnectionString: "mongodb://bluebirddx:password@mongo:27017/"
      Mongo__Database: "bluebirddx"
      Database__MongoExpressUrl: "/mongo"
      S3__ServiceUrl: "<S3 service URL>"
      S3__Bucket: "<S3 bucket name>"
      S3__AccessKey: "<S3 access key>"
      S3__SecretAccessKey: "<S3 access key secret>"
      TextWrapper__Server: "http://textwrapper"
      Grpc__CoreUrl: "http://core"
      SocialAppAuthorization__BaseUrl: "http://bluebird.example.com"
```

### Database Setup

Create the following collections underneath your specified database:

* `accounts`
* `media`
* `media_jobs`
* `threads`

### Adding Accounts

Before you can use BlueBirdDX, you need to add an account group to the `accounts` collection. There is no UI for this in the WebApp at this time, so you will need to do this manually using a MongoDB tool, like Mongo Express.

Use the following template to create an account group:

```
{
    _id: ObjectId(),
    SchemaVersion: 5,
    Name: 'Account Name',
    Twitter: null,
    Bluesky: {
        Identifier: 'identifier.example.com',
        Password: '<password>'
    },
    Mastodon: {
        InstanceUrl: 'https://fedi.example.com',
        AccessToken: '<access token>'
    },
    Threads: null,
    TwitterOAuth1: null,
    ThreadsLegacy: null
}
```

If you would like to exclude a certain social media site from a group, replace its entire object with `null`.

To create tokens for Twitter and Threads, use the Account Management dropdown to associate a social media account with an account group and get credentials for it. For Bluesky and Mastodon, insert the keys into the account group document in the fields shown above.

### Creating Threads

You can now access the WebApp!

On the home page, you can see all threads associated with the currently selected account. Change the account with the dropdown box. To create a thread, press the New Thread button. If you would like to upload media for use in a thread, click the Media Gallery link.

## API

An API library, `BlueBirdDX.Api`, is provided for those who want to automate usage of BlueBird.

First, create a `BlueBirdClient` instance:

```csharp
BlueBirdClient client = new BlueBirdClient("http://webapp");
```

You can then use the client to upload media and enqueue threads:

```csharp
// Upload some media.
CheckMediaUploadJobStateResponse checkResponse = await client.UploadMedia("Media Name", "image/jpeg", data, "Alt text"));

if (checkResponse.IsFailure())
{
    // Error handling.
}

// Create a new thread.
PostThreadApi thread = new PostThreadApi()
{
    Name = "Thread Name",
    TargetGroup = "670e0925255f23c9bdfcfa15", // target group ID
    PostToTwitter = true,
    PostToBluesky = true,
    PostToMastodon = true,
    PostToThreads = true,
    ScheduledTime = DateTime.UtcNow,
    Items = new List<PostThreadItemApi>()
    {
        new PostThreadItemApi()
        {
            Text = "Hello, World!",
            AttachedMedia = new List<string>()
            {
                checkResponse.Id
            }
        }
    }
};

// Post the thread.
await client.EnqueuePostThread(thread);
```

If you would like to have more control over the media upload process, you can use the media upload job APIs:

```csharp
// Create a new media upload job.
CreateMediaUploadJobResponse createResponse = await client.CreateMediaUploadJob("Media Name", "image/jpeg", "Alt text");

// Upload the image to the S3 bucket.
HttpClient httpClient = new HttpClient();
await httpClient.PutAsync(createResponse.TargetUrl, new ByteArrayContent(data));

// Set the job as ready for processing.
await client.SetMediaUploadJobAsReady(createResponse.Id);

// Wait for the media to be processed.
CheckMediaUploadJobStateResponse checkResponse = await client.WaitForMediaUploadJobToFinish(createResponse.Id);
```
