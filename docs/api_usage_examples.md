# API Usage Examples

Base URL: `http://localhost:5281`

## Health
```bash
curl -i http://localhost:5281/health/live
curl -i http://localhost:5281/health/ready
```

## Search
```bash
curl "http://localhost:5281/api/search?query=dune"
```

## Start Download
```bash
curl -X POST "http://localhost:5281/api/downloads/start" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"bookFormatId":1,"source":"dune"}'
```

## Track Download Status
```bash
curl "http://localhost:5281/api/downloads?userId=1"
curl "http://localhost:5281/api/downloads/1"
```

## Upsert Progress
```bash
curl -X PUT "http://localhost:5281/api/progress" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"bookId":1,"formatType":"text","positionRef":"c5:p10","progressPercent":42.0}'
```

## Add History Event
```bash
curl -X POST "http://localhost:5281/api/history" \
  -H "Content-Type: application/json" \
  -d '{"userId":1,"bookId":1,"formatType":"text","eventType":"completed","positionRef":"c20:p30"}'
```

## Mark Local Asset Deleted (Retention Safe)
```bash
curl -X DELETE "http://localhost:5281/api/assets/1?userId=1"
```

## Verify History/Progress Still Exist
```bash
curl "http://localhost:5281/api/history?userId=1&bookId=1"
curl "http://localhost:5281/api/progress?userId=1&bookId=1&formatType=text"
```
