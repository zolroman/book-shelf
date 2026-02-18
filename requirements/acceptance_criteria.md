# Acceptance Criteria (v1)

## 1. Search Metadata
Given user enters title and/or author  
When search is executed  
Then API returns normalized results from FantLab with catalog status badges.

## 2. Candidate Discovery
Given user opened a search result and selected media type  
When candidate request is executed  
Then API returns Jackett candidates with `downloadUri` and `sourceUrl`.

## 3. Add and Download
Given user selected a candidate  
When user clicks `Add to Library`  
Then book is upserted and a `download_jobs` row is created  
And download starts immediately through qBittorrent.

## 4. Manual Media Type Selection
Given both text and audio candidates exist  
When user chooses one type  
Then only selected media type is started for download.

## 5. Active Job Idempotency
Given active job exists for `(user, book, mediaType)`  
When same add-and-download action is repeated  
Then API returns existing active job  
And no duplicate active job is created.

## 6. Completion Transition
Given qBittorrent reports completed status  
When sync loop processes the job  
Then job becomes `completed`  
And media asset becomes available  
And book state is recomputed to `Library` if at least one media exists.

## 7. Not Found Grace Period
Given qBittorrent returns `not found` for active job  
When elapsed time is less than 1 minute  
Then job remains active  
When elapsed time reaches 1 minute  
Then job transitions to `failed` with reason `missing_external_job`.

## 8. Source URL Retention
Given media was downloaded with Jackett source URL  
When local file is deleted  
Then source URL remains stored in media asset metadata.

## 9. Offline Read/Listen
Given media is already downloaded locally  
When client is offline  
Then user can still read/listen  
And progress/history are stored locally for later sync.

## 10. Offline Add Behavior
Given client is offline  
When user tries add-and-download  
Then action is rejected with `NETWORK_REQUIRED`  
And no download job is created.
