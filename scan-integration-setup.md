# Integration testing setup
In order to test the complete integration of parts and especially the third party service integration libraries
As an architect
I want to be able to run tests against docker containers and mocked http API's.

## Requirements

### Technical
* YouTube API is mocked through mockserver docker container.
* HTTP Responses can either be predefined or runtime defined using AutoFixture and possible extensions.
* An Azurite docker container is set up for the storage queue.
* The functions run in their own docker container.
* The setup up of the infrastructure (environment) should be defined in a strategy so it allows for running tests against
actual infrastructure if needed.
* Containers environment setup can be shared over multiple integration test classes for maintainability and performance purposes.
* The initialization of the application container uses the environment setup output to configure the code using Env Variable Options Pattern.
* The integration tests are actual blackbox tests. The infrastructure containers described are the input/output of the
system under tests. The function HTTP endpointss are mostly the input and the responses & storage facilities the output.

### Acceptance Criteria Scenarios
Write the following test scenarios in a test in the InDepthDispenza.IntegrationTests project.

#### Video's queued successfully
Given a YouTube Playlist with ID T3sTV1d305 with 100 videos
When I scan that playlist through the `ScanPlaylist` endpoint
And I use a limit of 3
Then 3 messages should be stored on the Queue "videos"
And the IDs should match the first three
And the `ScanPlaylist` should return a succesful response.

#### Video's queuing failed
Given a YouTube Playlist with ID T3sTV1d305 with 100 videos
When I scan that playlist through the `ScanPlaylist` endpoint
And the API responds with a 429 failure with message "API Limit Reached"
Then the `ScanPlaylist` should return an unsuccesful response.