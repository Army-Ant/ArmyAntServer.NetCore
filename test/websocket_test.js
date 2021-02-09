'use strict'

const APPID = 1001;

let websocket_tester = {
	_ws:null,
	_loginName:null,
	_convIndex:1,
	_msgCodeCallbackList:null,
	
	checkIsBigEnding: function(){
		let buffer = new ArrayBuffer(8)
		let uint32 = new Uint32Array(buffer)
		uint32[0] =1 // 在uint32对应的缓冲区的开始，用四个字节，写入数字1 默认按计算机存储方式， 如果是小端存储，每一个缓冲区byte分别为 1，0，0，0. 大端存储为0,0,0,1
 
		let uint8 = new Uint8Array(buffer, 0, 1) //让uint8对应缓冲区的前1个字节，并按uint8 来呈现缓冲区
		return uint8 == 0
	},
	
	onload: function(){
		websocket_tester._msgCodeCallbackList = {
			10012001: websocket_tester.onloginResponse,
			10012002: websocket_tester.onlogoutResponse,
			10012003: websocket_tester.onsendResponse,
			10012004: websocket_tester.onbroadcastResponse,
			10012005: websocket_tester.onreceived,
			10012006: websocket_tester.onappError,
		}
	},
	
	writeMessage: function(msg){
		document.getElementById('message').innerHTML += '<br/>' + msg;
	},
	
	writeAlert: function(msg){
		window.alert(msg);
		this.writeMessage(msg);
	},
	
	connect: function(){
		document.getElementById('btnConnect').disabled=true
		document.getElementById('myusername').disabled=true
		if(this._ws){
			this._ws.close();
			this.writeMessage('断开服务器...');
		} else {
			this._ws = new WebSocket("ws://127.0.0.1:8080/");
			this._ws.onopen = function(evt) {
				this.writeMessage("WebSocket 服务器已连接");
				document.getElementById('btnConnect').innerHTML="断开服务器";
				document.getElementById('btnConnect').disabled=false
				this._loginName=null
				document.getElementById('btnLogin').disabled=false
				document.getElementById('btnSend').disabled=false
				document.getElementById('btnBroadcast').disabled=false
			}.bind(this);

			this._ws.onmessage = function(evt) {
				let reader = new FileReader();
				reader.onload = function(load_event){
					if(load_event.target.readyState == FileReader.DONE){
						let msg = JSON.parse(load_event.target.result);
						if(this._msgCodeCallbackList[msg.messageCode] != null){
							this._msgCodeCallbackList[msg.messageCode].bind(this)(msg)
						}else{
							this.writeMessage("收到不明消息:"+msg)
						}
					}
				}.bind(this);
				reader.readAsText(evt.data);
			}.bind(this);
			this._ws.onerror = function(evt) {
				this.writeMessage("Websocket caused error: " + evt);
			}.bind(this);

			this._ws.onclose = function(evt) {
				this.writeMessage("WebSocket 服务器已断开");
				this._ws = null;
				document.getElementById('btnConnect').innerHTML="连接服务器";
				document.getElementById('btnConnect').disabled=false
				document.getElementById('myusername').disabled=false
				this._loginName=null
				document.getElementById('btnLogin').innerHTML="登录";
				document.getElementById('btnLogin').disabled=true
				document.getElementById('btnSend').disabled=true
				document.getElementById('btnBroadcast').disabled=true
			}.bind(this);
			this.writeMessage('连接服务器...');
		}
	},
	
	login: function(){
		if(document.getElementById('myusername').value=="" || !document.getElementById('myusername').value){
			this.writeAlert("请输入登录用户名")
			return;
		}
		if(this._ws){
			if(this._loginName==null){
				let msg = 
				{
					appId: APPID,
					messageCode: 10011001,
					conversationCode: ++this._convIndex,
					conversationStepIndex: 0,
					conversationStepType: 2,
					userName: document.getElementById('myusername').value,
				}				
				this._ws.send(JSON.stringify(msg));
				this.writeMessage("登录中...");
			}else{
				let msg = 
				{
					appId: APPID,
					messageCode: 10011002,
					conversationCode: ++this._convIndex,
					conversationStepIndex: 0,
					conversationStepType: 2,
					userName: document.getElementById('myusername').value,
				}
				this._ws.send(JSON.stringify(msg));
				this.writeMessage("登出...");
			}
		} else {
			this.writeAlert("Websocket 尚未建立连接, 请先连接服务器");
		}
	},
	
	send: function(){
		if(document.getElementById('tarusername').value=="" || !document.getElementById('tarusername').value){
			this.writeAlert("请输入发送消息目标的用户名");
			return;
		}
		if(document.getElementById('sendingcontent').value=="" || !document.getElementById('sendingcontent').value){
			this.writeAlert("请输入要发送的消息内容");
			return;
		}
		if(this._ws){
			if(this._loginName==null){
				this.writeAlert("尚未登录, 请先登录");
			}else{
				let msg = 
				{
					appId: APPID,
					messageCode: 10011003,
					conversationCode: ++this._convIndex,
					conversationStepIndex: 0,
					conversationStepType: 2,
					target: document.getElementById('tarusername').value,
					message: document.getElementById('sendingcontent').value,
				}
				this._ws.send(JSON.stringify(msg));
				this.writeMessage("发送消息中...");
			}
		} else {
			this.writeAlert("Websocket 尚未建立连接, 请先连接服务器");
		}
	},
	
	broadcast: function(){
		if(document.getElementById('sendingcontent').value=="" || !document.getElementById('sendingcontent').value){
			this.writeAlert("请输入要发送的消息内容");
			return;
		}
		if(this._ws){
			if(this._loginName==null){
				this.writeAlert("尚未登录, 请先登录");
			}else{
				let msg = 
				{
					appId: APPID,
					messageCode: 10011004,
					conversationCode: ++this._convIndex,
					conversationStepIndex: 0,
					conversationStepType: 2,
					message: document.getElementById('sendingcontent').value,
				}
				this._ws.send(JSON.stringify(msg));
				this.writeMessage("发送广播中...")
			}
		} else {
			this.writeAlert("Websocket 尚未建立连接, 请先连接服务器");
		}
	},
	
	onloginResponse: function(msg){
		if(msg.result==0){
			this.writeMessage("登录成功!");
			document.getElementById('btnLogin').innerHTML="登出";
			this._loginName=msg.message
		}else{
			this.writeMessage("登录失败, 信息:"+msg.message);
		}
	},
	
	onlogoutResponse: function(msg){
		if(msg.result==0){
			this.writeMessage("登出成功!");
			document.getElementById('btnLogin').innerHTML="登录";
			this._loginName=null
		}else{
			this.writeMessage("登出失败, 信息:"+msg.message);
		}
	},
	
	onsendResponse: function(msg){
		if(msg.result==0){
			this.writeMessage("向"+msg.request.target+"发送消息成功!");
		}else{
			this.writeMessage("发送消息失败, 信息:"+msg.message);
		}
	},
	
	onbroadcastResponse: function(msg){
		if(msg.result==0){
			this.writeMessage("发送广播成功!");
		}else{
			this.writeMessage("发送广播失败, 信息:"+msg.message);
		}
	},
	
	onreceived: function(msg){
		if(msg.is_broadcast){
			this.writeMessage("["+msg.from+"] [广播] "+msg.message);
		}else{
			this.writeMessage("["+msg.from+"] "+msg.message);
		}
	},
	
	onappError: function(msg){
		this.writeMessage("收到服务器的错误报告, 错误代码!"+msg.error_code+", 错误消息:"+msg.message);
	}
}

export default websocket_tester;
