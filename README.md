# ZephiX-Douyu-Chat-Message-Lite

## 斗鱼直播弹幕抓取 -- 精简版  c# lib
### 2021.08

目前仅支持弹幕消息接收

# 使用方法(参考 testconsole 演示)

### 1. 添加引用
``` using Douyu; ```

### 2.创建实例
```DouyuLiveChat dy = new();```

### 3.连接直播间(直播间号)
```dy.Connect(216911);```

### 4.订阅消息事件
```dy.onLiveMessageReceived += onMessage;```

### 消息事件delegate定义如下:
```public delegate void LiveChatMessageHandler(LiveChatMessage message);```

### LiveChatMessage记录定义如下
```public record LiveChatMessage(int RoomID, string Platform, string CID, string Content, int FromUID, string FromNickName, int FromLv, string FromBadge, int BadgeLv, string Icon, int TimeStamp);```

参数|类型|描述
------------ | ------------- | -------------
RoomID | int | 直播间房间号
Platform | string | 固定为"douyutv"请无视
CID | string | 弹幕独立ID 可用作key
Content | string | 弹幕消息内容
FromUID | int | 弹幕发送人UserID
FromNickName | string | 弹幕发送人昵称
FromLv | int | 弹幕发送人等级
FromBadge | string | 弹幕发送人佩戴粉丝徽章
BadgeLv | int | 弹幕发送人粉丝徽章等级
Icon | string | 弹幕发送人头像(小)URL
Timestamp | int | 当前时间Unix时间戳


## Email: ZeffiX@qq.com
## QQ: 8708534  

欢迎小伙伴联系交流

### 本程序仅为学习交流使用，请合法使用，禁止用于商业用途，转载请标明来源与作者信息
