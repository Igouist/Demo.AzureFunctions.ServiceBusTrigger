# 菜雞抓蟲：Azure Functions ServiceBus Trigger 執行過久時會重複觸發 Functions
在 Azure Functions Isolated 測試 Service Bus Trigger 的範例專案

> 本文同步發表於部落格（好讀版 →）：https://igouist.github.io/post/2022/08/azure-function-servicebus-trigger-max-auto-renew-duration/

![Image](https://i.imgur.com/00WQGqR.png)

## TL;DR

當發現**需要執行很久的 ServiceBus Trigger Function 有重複執行的情況**出現時，可以嘗試到[官方的 Host.json 設定指引](https://docs.microsoft.com/zh-tw/azure/azure-functions/functions-bindings-service-bus?tabs=in-process%2Cextensionv5%2Cextensionv3&pivots=programming-language-csharp#hostjson-settings)，按照 SDK 版本找到對應的「**訊息鎖定最大持續時間**」設定，例如 maxAutoLockRenewalDuration（延伸模組 5.x+）或 maxAutoRenewDuration（Functions 2.x），並加入專案的 Host.json

**因為 ServiceBus 在傳遞訊息之後，如果超過一段時間（MaxAutoRenewDuration）內沒有得到回應，就會解除信件的鎖並嘗試重新傳遞**，這時候如果原先的 Function 仍在執行，就會一前一後重複執行 Function 並發生許多光怪陸離的事，例如寫入兩筆資訊、重複複製資料之類的。

建議如果調整了有 ServiceBus Trigger Function 的 Azure Functions Timeout 設定時，或是發現某支 ServiceBus Trigger 的 Functions 執行時間過長，就要一併注意 MaxAutoRenewDuration 的設定，避免重複執行的情況出現。

<!--more-->

## 事發原由

工作時將一段需要呼叫其他 API、執行相當久的程式片段搬上 [Azure Functions](/post/2022/09/bus-reminder-2-azure-functions-timetrigger-with-line-notify/)，並使用 [Service Bus](/post/2022/08/azure-service-bus) 來傳遞訊息觸發 Functions 執行，這時卻發現 **Function 被執行了兩次**！

現在就讓我們來重建當時的情況吧。首先我們有個 Service Bus Trigger 的 Azure Function，這邊就直接從 Visual Studio 提供的[已隔離（Isolated）](https://docs.microsoft.com/zh-tw/azure/azure-functions/dotnet-isolated-process-guide)範本進行建立。

為了重現執行很久的特點，我們讓它 Delay 個八分鐘，並在開始和結束的時候告訴我們一下：
```csharp
/// <summary>
/// ServiceBus Trigger 測試用 Function
/// </summary>
/// <param name="myQueueItem">My queue item.</param>
[Function("ServiceBusTriggerSample")]
public async Task Run(
    [ServiceBusTrigger(
        queueName: "%QueueName%",
        Connection = "ServiceBus")]
    string myQueueItem)
{
    _logger.LogInformation("開始處理訊息: {myQueueItem}", myQueueItem);

    await Task.Delay(new TimeSpan(0, 8, 0));
    
    _logger.LogInformation("結束處理訊息: {myQueueItem}", myQueueItem);
}
```

並且簡單地用之前 [Service Bus 文章](/post/2022/08/azure-service-bus) 的範例送個 "Hello" 進去 Queue 裡，準備觸發 Function：

```csharp
async Task Main()
{
	var context = "Hello!";
	
	await using var client = new ServiceBusClient(_connectionString);
	await using var sender = client.CreateSender(_queueName);
	var message = new ServiceBusMessage(context);
	await sender.SendMessageAsync(message);
}
```

在 Function 接收到訊息後，讓我們觀察 Console：

![Image](https://i.imgur.com/dBFWz2p.png)

**可以發現第一次執行尚未結束的時候，大概經過五分鐘就又執行了第二次！**

## 調整訊息鎖定最大持續時間

原先以為是 Function 執行失敗導致 ServiceBus 重新傳遞之類的狀況，但找了老半天沒有頭緒，嘗試了調整一些設定也沒有起色，陷入了深深的混亂

![Image](https://i.imgur.com/UY3EhoA.png)

幸好天無絕人之路，最終在 [Stackoverflow](https://stackoverflow.com/questions/62752905/azure-function-service-bus-trigger-running-multiple-times) 海巡的時候了發現一線生機！

原來 Azure Functions 的 Service Bus Trigger 有個「**訊息鎖定最大持續時間**」設定！

當 Service Bus 傳遞訊息到 Function 的時候，Function 會根據執行結果告訴 Service Bus 該訊息要標記成功或是失敗；但如果訊息就這麼一去不回時，Service Bus 會先觀望一下，**直到超過了「訊息鎖定最大持續時間」就會下令解除訊息的鎖定，嘗試重新傳遞**。

這次的事件就是因為我們即使調長了 Timeout 時間，但當該 Function 執行超過預設的鎖定時間（五分鐘）時，Service Bus 再度傳遞了訊息，才導致 Function 重複被執行而造成各種奇怪的資料錯誤

那麼這個「訊息鎖定最大持續時間」怎麼設定呢？我們可以參照[官方的 Host.json 設定指引](https://docs.microsoft.com/zh-tw/azure/azure-functions/functions-bindings-service-bus?tabs=in-process%2Cextensionv5%2Cextensionv3&pivots=programming-language-csharp#hostjson-settings)，按照 SDK 版本找到對應的「訊息鎖定最大持續時間」設定，例如 maxAutoLockRenewalDuration（延伸模組 5.x+）或 maxAutoRenewDuration（Functions 2.x），並加入專案的 Host.json。

現在讓我們在範例專案加入 maxAutoRenewDuration 的設定，這邊就改成比前面的執行時間八分鐘更長的十分鐘：

![Image](https://i.imgur.com/TTpXELd.png)

接著再重新傳遞一次訊息：

![Image](https://i.imgur.com/2epGiF9.png)

可以看到過程中沒有重新傳遞訊息了，大功告成！

總之，學到了調整 ServiceBus Trigger Function 的 Azure Functions Timeout 設定時，或是發現某支 ServiceBus Trigger 的 Functions 執行時間過長，就要一併注意 MaxAutoRenewDuration 的設定，避免重複執行。

如此如此，這般這般，一天又平安的過去了，感謝 Stackoverflow 大大們的努力。

## 參考資料

- [Azure function service bus trigger running multiple times - Stockoverflow](https://stackoverflow.com/questions/62752905/azure-function-service-bus-trigger-running-multiple-times)
- [Azure Functions 的 Azure 服務匯流排繫結 | Microsoft Docs](https://docs.microsoft.com/zh-tw/azure/azure-functions/functions-bindings-service-bus?tabs=in-process%2Cfunctionsv2%2Cextensionv3&pivots=programming-language-csharp#hostjson-settings)
