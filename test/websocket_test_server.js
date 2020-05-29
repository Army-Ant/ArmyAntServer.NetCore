/**
 * Created by Jason on 2016/5/31.
 */
"use strict";
import libArmyAnt from "./ArmyAnt.js/libArmyAnt.js"

let serverHost = {
    onStart: function () {
        let svr = new libArmyAnt.HttpServer();
        let ret = svr.start(8088);
    },

    onTest: function () {

    }
};

serverHost.onStart();