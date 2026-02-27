# План исполнения: Library -> Details + рейтинги

## Краткое резюме
1. Путь для плана: `docs/library-details-execution-plan.md`.
2. Реализация: клик по названию в Library ведёт на существующую страницу деталей.
3. В деталях: обложка, название, оригинальное название (если отличается), авторы, год написания, общий рейтинг (числом), цикл (если есть), описание.
4. Пользовательский рейтинг: локальная БД, звёзды 1..5, можно поставить/изменить/снять.

## Публичные интерфейсы
1. `SearchBookDetailsResponse` расширяется nullable-полями: `CatalogBookId`, `WritingYear`, `OverallRating`, `Cycle`, `UserRating`.
2. Добавляются DTO: `SearchCycleDto`, `UpsertBookRatingRequest`, `BookUserRatingDto`.
3. Добавляются endpoints:
   - `PUT /api/v1/books/{bookId}/rating`
   - `DELETE /api/v1/books/{bookId}/rating`
4. `IBookshelfApiClient` расширяется методами `UpsertBookRatingAsync` и `DeleteBookRatingAsync`.

## Фаза 1: backend
1. Обновить контракты и provider models.
2. Расширить парсинг FantLab (`work_name_orig`, `work_description`, `work_year_of_write/work_year`, общий рейтинг, цикл, image/image_preview).
3. Обновить `BookSearchService` (новые поля + `CatalogBookId`).
4. Добавить сущность `BookRating`, таблицу `book_ratings`, миграцию, репозиторий, сервис, DI.
5. Добавить API PUT/DELETE rating и обогащение details полем `UserRating`.

## Фаза 2: frontend
1. `Library.razor`: кликабельно только название книги, переход на details с `returnUrl`.
2. Добавить методы rating в API clients.
3. `BookDetails.razor`: добавить требуемые поля и UI рейтинга звёздами.
4. `app.css`: стили details + star-rating.

## Фаза 3: тесты
1. Обновить/добавить unit tests для parser/service/rating.
2. Обновить API contract tests для PUT/DELETE rating и выдачи `UserRating`.
3. Проверить миграцию.
4. Прогнать build + тесты (`Infrastructure`, `Application`, `Api`).

## Фаза 4: review + e2e
1. findings-first review.
2. web flow: library -> details -> rating set/change/clear -> back.

## Допущения
1. Шкала user rating: 1..5 (целые).
2. Снятие оценки обязательно.
3. Общий рейтинг только из FantLab.
4. Дата написания отображается как год.
5. Маршрут деталей: `/books/{providerCode}/{providerBookKey}`.
