---

## SKILL.md

---
name: everything-integration
description: "Интеграция с индексом Everything для получения списка файлов и папок. Используется для построения Treemap-визуализации без собственного сканирования диска."
license: MIT
compatibility: ".NET Framework 4.8, Windows 7+"
metadata:
  author: "VoidSniffer Team"
  version: "1.0"
---

# Skill: Everything Integration

## Назначение
Навык позволяет AI-агенту эффективно использовать индекс Everything (voidtools) для получения информации о файловой системе вместо прямого сканирования.

## Когда использовать
- При построении списка файлов и папок для визуализации
- При необходимости быстрого получения данных о размерах файлов
- При фильтрации файлов по имени, расширению, размеру

## Ключевые возможности Everything

### 1. Поиск файлов и папок
Everything индексирует все файлы и папки на NTFS-дисках. Запрос через SDK возвращает:
- Имя файла/папки
- Полный путь
- Размер (если включено в индексе)
- Дату изменения

### 2. IPC механизм
Everything SDK использует IPC (Inter-Process Communication) через `WM_COPYDATA`. Это означает, что:
- Everything должен быть запущен в фоне[reference:0]
- SDK предоставляет DLL и Lib интерфейс для C/C++[reference:1]
- Для .NET доступны managed-обёртки

### 3. Режимы работы
- **Blocking mode** — запрос блокирует поток до получения результатов
- **Non-blocking mode** — запрос возвращает управление немедленно, результаты проверяются позже[reference:2]

## C# реализации

### Вариант 1: EverythingSearchClient (managed, без native DLL)
Рекомендуемый подход — чисто managed библиотека, использующая message-only window и IPC[reference:3].


```c#
using EverythingSearchClient;

public class EverythingDataProvider
{
    private readonly SearchClient _client = new();
    
    public async Task<List<FileSystemItem>> GetItemsAsync(string path)
    {
        // Поиск всех файлов и папок в указанном пути
        var result = _client.Search(
            query: $"\"{path}\\\\\"",  // поиск по пути
            flags: SearchFlags.MatchPath,
            maxResults: 100000,       // ограничение
            whenBusy: BehaviorWhenBusy.WaitOrError
        );
    
        return result.Items.Select(item => new FileSystemItem
        {
            Name = item.Name,
            Path = item.Path,
            IsFolder = item.Flags.HasFlag(Result.ItemFlags.Folder),
            Size = 0  // размер нужно получать отдельно
        }).ToList();
    }
}
```
**Преимущества:** нет зависимости от native DLL, AnyCPU, простой интерфейс.

### Вариант 2: Прямой P/Invoke Everything64.dll

Если нужен полный доступ к API:

```c#
[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
public static extern void Everything_SetSearchW(string lpSearchString);

[DllImport("Everything64.dll")]
public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

[DllImport("Everything64.dll")]
public static extern bool Everything_QueryW(bool bWait);

[DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
public static extern void Everything_GetResultFullPathNameW(
    int nIndex, 
    StringBuilder lpString, 
    int nMaxCount);
```

**Недостатки:** нужны отдельные сборки для x86/x64, сложнее в поддержке.

### Вариант 3: [Voidtools.Everything.Net](https://voidtools.everything.net/)

Typed .NET wrapper с поддержкой DI и x64/ARM64.

## Получение размеров файлов

**Важно:** Everything SDK не всегда возвращает размеры файлов в основном запросе. Для получения размеров необходимо:

1. Включить `Everything_SetRequestFlags(EVERYTHING_REQUEST_SIZE)` перед запросом
2. Использовать `Everything_GetResultSize` для каждого элемента

```c#
Everything_SetRequestFlags(EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME | EVERYTHING_REQUEST_SIZE);
Everything_Query(true);

for (int i = 0; i < Everything_GetNumResults(); i++)
{
    long size;
    Everything_GetResultSize(i, out size);
    // ...
}
```

## Построение иерархии

Everything возвращает **плоский список**. Для Treemap-визуализации необходимо построить дерево:

```c#
public class FileSystemTreeBuilder
{
    public TreeNode BuildTree(List<FileSystemItem> items, string rootPath)
    {
        var root = new TreeNode { Name = rootPath, IsFolder = true };
        var lookup = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        lookup[rootPath] = root;

        foreach (var item in items.OrderBy(i => i.Path))
        {
            var parentPath = Path.GetDirectoryName(item.Path);
            if (!lookup.TryGetValue(parentPath, out var parent))
            {
                // Создать отсутствующих родителей
                parent = CreateMissingParents(lookup, parentPath, rootPath);
            }

            var node = new TreeNode { Name = item.Name, Size = item.Size, IsFolder = item.IsFolder };
            parent.Children.Add(node);
            if (item.IsFolder) lookup[item.Path] = node;
        }

        CalculateAggregateSizes(root);
        return root;
    }
}
```

## Обработка больших объёмов

- Ограничивайте `maxResults` (например, 200000)
- Используйте `offset` для пагинации
- Для папок с миллионами файлов — запрашивайте рекурсивно по подпапкам
- Кэшируйте результаты на время навигации

## Обработка ошибок

| Ошибка                 | Причина                 | Решение                                     |
| ---------------------- | ----------------------- | ------------------------------------------- |
| Everything not running | Сервис не запущен       | Показать сообщение, предложить запустить    |
| Timeout                | База данных загружается | Использовать `BehaviorWhenBusy.WaitOrError` |
| Access denied          | Нет прав на папку       | Пропустить элемент, логировать              |
| Empty result           | Неверный запрос         | Проверить синтаксис поиска Everything       |

## Рекомендуемые NuGet-пакеты

1. **EverythingSearchClient** (sgrottel) — managed, AnyCPU, простой API
2. **Voidtools.Everything.Net** — typed wrapper, DI support
3. **EverythingNet** — fluent .NET API

## Ссылки

- [Everything SDK Documentation](https://www.voidtools.com/support/everything/sdk/)
- [Everything IPC](https://www.voidtools.com/support/everything/sdk/ipc/)
- [EverythingSearchClient GitHub](https://github.com/sgrottel/EverythingSearchClient)