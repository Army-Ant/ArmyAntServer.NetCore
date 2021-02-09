/**
 * Created by Jason on 2016/5/31.
 */
"use strict";
import HTTPServer from "./ArmyAnt.js/scripts/node.js_extend/httpServer"

let serverHost = {
    onStart: function () {
        let svr = new HTTPServer();
        svr.start("./", 8088);
    },

    onTest: function () {

    }
};

serverHost.onStart();