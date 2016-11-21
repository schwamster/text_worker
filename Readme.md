# text-worker

This project is a subset of https://github.com/schwamster/docStack


## Running on Mac:

1. Install dotnetcore and required OpenSSL workarounds as specified by the dotnetcore team here: https://www.microsoft.com/net/core#macos

2. Use the compose file to create the image and start container:
`docker-compose -f docker-compose-build up`  
(if build.sh throws formatting errors due to the presence of carriage returns caused by Windows text editors), run:
    - `brew install dos2unix`
    - `dos2unix build.sh`
