# BlueBirdDX.TextWrapper

This is a small node application that allows other applications to call certain twitter-text APIs through REST endpoints.

Yes, this is a bit overkill. I tried creating a C# version of twitter-text, but I encountered multiple problems when porting the various regular expressions. In the end, I decided to do this instead to make my life easier. (It also has the benefit of being guaranteed to always give the correct output, which a theoretical C# reimplementation may not have.)
