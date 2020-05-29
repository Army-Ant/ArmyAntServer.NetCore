'use strict'

import libArmyAnt from "./ArmyAnt.js/libArmyAnt.js"

let websocket_tester = {
	_ws:null,
	_loginName:null,
	_convIndex:1,
	_scriptList:[
		{script: "libprotobuf-js/map.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/message.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/constants.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/utils.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/decoder.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/encoder.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/arith.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/reader.js", isAsync: false, isDefer: false},
		{script: "libprotobuf-js/binary/writer.js", isAsync: false, isDefer: false},
		{script: "proto-js/aaserver_proto.js", isAsync: false, isDefer: false},
	],
	
	echoServerRequiring: function(){
		let loadScript = (i) => {
			if(i >= this._scriptList.length)
				return;
			let scriptName = this._scriptList[i].script;
			let isAsync = false
			if(this._scriptList[i].isAsync)
				isAsync = this._scriptList[i].isAsync
			let isDefer = false
			if(this._scriptList[i].isDefer)
				isDefer = this._scriptList[i].isDefer
			libArmyAnt.importScript(scriptName, isAsync, isDefer, ()=>{loadScript(i+1);})
			//goog.require(this._scriptList[i])
		}
		loadScript(0);
	},
	
	checkIsBigEnding: function(){
		let buffer = new ArrayBuffer(8)
		let uint32 = new Uint32Array(buffer)
		uint32[0] =1 // 在uint32对应的缓冲区的开始，用四个字节，写入数字1 默认按计算机存储方式， 如果是小端存储，每一个缓冲区byte分别为 1，0，0，0. 大端存储为0,0,0,1
 
		let uint8 = new Uint8Array(buffer, 0, 1) //让uint8对应缓冲区的前1个字节，并按uint8 来呈现缓冲区
		return uint8 == 0
	},
	
	createHead: function(serials, type, extendVersion, extend, content){
		let isBig = this.checkIsBigEnding()
		let buffer = new ArrayBuffer(16)
		let arr32 = new Uint32Array(buffer);
		if(isBig){
			arr32[0] = extend.length
			arr32[1] = extendVersion
			arr32[2] = type
			arr32[3] = serials
			let ret = Array.prototype.slice.call(new Uint8Array(buffer))
			return ret.reverse()
		} else {
			arr32[0] = serials
			arr32[1] = type
			arr32[2] = extendVersion
			arr32[3] = extend.length
			let ret = Array.prototype.slice.call(new Uint8Array(buffer))
			return ret
		}
	},
	
	deserializeData: function(dataWriter){
		let ret = {}
		let isBig = this.checkIsBigEnding()
		let uint8arr = new Uint8Array(dataWriter);
		
		let headBuffer = new ArrayBuffer(16)
		let head_arr8 = new Uint8Array(headBuffer);
		for(let i=0; i<16; ++i){
			head_arr8[i] = uint8arr[i];
		}
		let head_arr32 = new Uint32Array(headBuffer);
		if(isBig){
			let head = Array.prototype.slice.call(head_arr8).reverse()
			let reversed = Uint32Array.from(head)
			ret.extendLength = reversed[0]
			ret.extendVersion = reversed[1]
			ret.type = reversed[2]
			ret.serials = reversed[3]
		} else {
			ret.serials = head_arr32[0]
			ret.type = head_arr32[1]
			ret.extendVersion = head_arr32[2]
			ret.extendLength = head_arr32[3]
		}
		
		let extendBuffer = new ArrayBuffer(ret.extendLength)
		let extend_arr8 = new Uint8Array(extendBuffer);
		for(let i=16; i<16+ret.extendLength; ++i){
			extend_arr8[i-16] = uint8arr[i];
		}
		ret.extend = proto.ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1.deserializeBinary(Array.prototype.slice.call(extend_arr8))
		
		let dataBuffer = new ArrayBuffer(ret.extend.getContentLength())
		let data_arr8 = new Uint8Array(dataBuffer);
		for(let i=16+ret.extendLength; i<16+ret.extendLength+ret.extend.getContentLength(); ++i){
			data_arr8[i-16-ret.extendLength] = uint8arr[i];
		}
		ret.contentMessageBytes = Array.prototype.slice.call(data_arr8)
		
		return ret
	},
	
	onload: function(){
	},
	
	writeMessage: function(msg){
		document.getElementById('message').innerHTML += '<br/>' + msg;
	},
	
	writeAlert: function(msg){
		window.alert(msg);
		this.writeMessage(msg);
	},
	
	ab2str: function(buf) {
		return String.fromCharCode.apply(null, new Uint16Array(buf));
	},

	str2ab: function(str) {
		var buf = new ArrayBuffer(str.length); // 2 bytes for each char
		var bufView = new Uint8Array(buf);
		for (var i=0, strLen=str.length; i<strLen; i++) {
			bufView[i] = str.charCodeAt(i);
		}
		return buf;
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
						let data = load_event.target.result;
						let decodeRes = this.deserializeData(this.str2ab(data));
						let msg = null
						switch(decodeRes.extend.getMessageCode()){
							case 10012001:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoLoginResponse.deserializeBinary(decodeRes.contentMessageBytes)
								this.onloginResponse(msg)
								break;
							case 10012002:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoLogoutResponse.deserializeBinary(decodeRes.contentMessageBytes)
								this.onlogoutResponse(msg)
								break;
							case 10012003:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoSendResponse.deserializeBinary(decodeRes.contentMessageBytes)
								this.onsendResponse(msg)
								break;
							case 10012004:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoBroadcastResponse.deserializeBinary(decodeRes.contentMessageBytes)
								this.onbroadcastResponse(msg)
								break;
							case 10011005:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoReceiveNotice.deserializeBinary(decodeRes.contentMessageBytes)
								this.onreceived(msg)
								break;
							case 10011006:
								msg = proto.ArmyAntMessage.SubApps.SM2C_EchoError.deserializeBinary(decodeRes.contentMessageBytes)
								this.onappError(msg)
								break;
							default:
								this.writeMessage("收到不明消息:"+decodeRes.contentMessageBytes)
						}
					}
				}.bind(this);
				reader.readAsBinaryString(evt.data);
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
				let msg = new proto.ArmyAntMessage.SubApps.C2SM_EchoLoginRequest()
				msg.setUserName(document.getElementById('myusername').value)
				let bytes_msg = msg.serializeBinary()
				let extend = new proto.ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1()
				extend.setAppId(1001)
				extend.setContentLength(bytes_msg.length)
				extend.setMessageCode(10011001)
				extend.setConversationCode(++this._convIndex)
				extend.setConversationStepIndex(0)
				extend.setConversationStepType(proto.ArmyAntMessage.System.ConversationStepType.ASKFOR)
				let bytes_extend = extend.serializeBinary()
				let byteArray_head = this.createHead(0, 1, 1, bytes_extend, bytes_msg)
				let array_all = byteArray_head.concat(Array.prototype.slice.call(bytes_extend), Array.prototype.slice.call(bytes_msg))
				let writer_all = Uint8Array.from(array_all)
				this._ws.send(writer_all);
				this.writeMessage("登录中...");
			}else{
				let msg = new proto.ArmyAntMessage.SubApps.C2SM_EchoLogoutRequest()
				msg.setUserName(document.getElementById('myusername').value)
				let bytes_msg = msg.serializeBinary()
				let extend = new proto.ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1()
				extend.setAppId(1001)
				extend.setContentLength(bytes_msg.length)
				extend.setMessageCode(10011002)
				extend.setConversationCode(++this._convIndex)
				extend.setConversationStepIndex(0)
				extend.setConversationStepType(proto.ArmyAntMessage.System.ConversationStepType.ASKFOR)
				let bytes_extend = extend.serializeBinary()
				let byteArray_head = this.createHead(0, 1, 1, bytes_extend, bytes_msg)
				let array_all = byteArray_head.concat(Array.prototype.slice.call(bytes_extend), Array.prototype.slice.call(bytes_msg))
				let writer_all = Uint8Array.from(array_all)
				this._ws.send(writer_all);
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
				let msg = new proto.ArmyAntMessage.SubApps.C2SM_EchoSendRequest()
				msg.setTarget(document.getElementById('tarusername').value)
				msg.setMessage(document.getElementById('sendingcontent').value)
				let bytes_msg = msg.serializeBinary()
				let extend = new proto.ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1()
				extend.setAppId(1001)
				extend.setContentLength(bytes_msg.length)
				extend.setMessageCode(10011003)
				extend.setConversationCode(++this._convIndex)
				extend.setConversationStepIndex(0)
				extend.setConversationStepType(proto.ArmyAntMessage.System.ConversationStepType.ASKFOR)
				let bytes_extend = extend.serializeBinary()
				let byteArray_head = this.createHead(0, 1, 1, bytes_extend, bytes_msg)
				let array_all = byteArray_head.concat(Array.prototype.slice.call(bytes_extend), Array.prototype.slice.call(bytes_msg))
				let writer_all = Uint8Array.from(array_all)
				this._ws.send(writer_all);
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
				let msg = new proto.ArmyAntMessage.SubApps.C2SM_EchoBroadcastRequest()
				msg.setMessage(document.getElementById('sendingcontent').value)
				let bytes_msg = msg.serializeBinary()
				let extend = new proto.ArmyAntMessage.System.SocketExtendNormal_V0_0_0_1()
				extend.setAppId(1001)
				extend.setContentLength(bytes_msg.length)
				extend.setMessageCode(10011004)
				extend.setConversationCode(++this._convIndex)
				extend.setConversationStepIndex(0)
				extend.setConversationStepType(proto.ArmyAntMessage.System.ConversationStepType.ASKFOR)
				let bytes_extend = extend.serializeBinary()
				let byteArray_head = this.createHead(0, 1, 1, bytes_extend, bytes_msg)
				let array_all = byteArray_head.concat(Array.prototype.slice.call(bytes_extend), Array.prototype.slice.call(bytes_msg))
				let writer_all = Uint8Array.from(array_all)
				this._ws.send(writer_all);
				this.writeMessage("发送广播中...")
			}
		} else {
			this.writeAlert("Websocket 尚未建立连接, 请先连接服务器");
		}
	},
	
	onloginResponse: function(msg){
		if(msg.getResult()==0){
			this.writeMessage("登录成功!");
			document.getElementById('btnLogin').innerHTML="登出";
			this._loginName=msg.getMessage()
		}else{
			this.writeMessage("登录失败, 信息:"+msg.getMessage());
		}
	},
	
	onlogoutResponse: function(msg){
		if(msg.getResult()==0){
			this.writeMessage("登出成功!");
			document.getElementById('btnLogin').innerHTML="登录";
			this._loginName=null
		}else{
			this.writeMessage("登出失败, 信息:"+msg.getMessage());
		}
	},
	
	onsendResponse: function(msg){
		if(msg.getResult()==0){
			this.writeMessage("向"+msg.getRequest().getTarget()+"发送消息成功!");
		}else{
			this.writeMessage("发送消息失败, 信息:"+msg.getMessage());
		}
	},
	
	onbroadcastResponse: function(msg){
		if(msg.getResult()==0){
			this.writeMessage("发送广播成功!");
		}else{
			this.writeMessage("发送广播失败, 信息:"+msg.getMessage());
		}
	},
	
	onreceived: function(msg){
		if(msg.getIsBroadcast()){
			this.writeMessage("["+msg.getFrom()+"] [广播] "+msg.getMessage());
		}else{
			this.writeMessage("["+msg.getFrom()+"] "+msg.getMessage());
		}
	},
	
	onappError: function(msg){
		this.writeMessage("收到服务器的错误报告, 错误代码!"+msg.getErrorCode()+", 错误消息:"+msg.getMessage());
	}
}

websocket_tester.echoServerRequiring();

export default websocket_tester;
