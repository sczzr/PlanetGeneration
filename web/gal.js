var _0x45d0 = ["\x57\x47\x47\x61\x6C", "\x50\x47\x47\x61\x6C", "\x54\x47\x47\x61\x6C", "\x48\x4D\x56\x47\x61\x6C", "\x57\x47\x54\x61\x62", "\x50\x47\x54\x61\x62", "\x54\x47\x54\x61\x62", "\x48\x4D\x56\x54\x61\x62", "\x6C\x65\x6E\x67\x74\x68", "\x6D\x61\x72\x67\x69\x6E", "\x73\x74\x79\x6C\x65", "\x67\x65\x74\x45\x6C\x65\x6D\x65\x6E\x74\x42\x79\x49\x64", "\x30\x20", "\x69\x6E\x6E\x65\x72\x57\x69\x64\x74\x68", "\x70\x78\x20\x30\x70\x78\x20", "\x70\x78", "\x64\x69\x73\x70\x6C\x61\x79", "", "\x62\x61\x63\x6B\x67\x72\x6F\x75\x6E\x64", "\x23\x32\x37\x38\x42\x44\x31", "\x73\x6C\x69\x63\x65", "\x69\x6E\x64\x65\x78\x4F\x66", "\x73\x70\x6C\x69\x63\x65", "\x6E\x6F\x6E\x65", "\x68\x65\x69\x67\x68\x74", "\x6D\x61\x69\x6E", "\x69\x6E\x6E\x65\x72\x48\x65\x69\x67\x68\x74", "\x6F\x6E\x72\x65\x73\x69\x7A\x65"];
var galList = [_0x45d0[0], _0x45d0[1], _0x45d0[2], _0x45d0[3]];
var tabList = [_0x45d0[4], _0x45d0[5], _0x45d0[6], _0x45d0[7]];
selectTab(_0x45d0[0], _0x45d0[4]);
//setGalWidth();

function setGalWidth() {
    for (var _0x344ax4 = 0; _0x344ax4 < galList[_0x45d0[8]]; _0x344ax4++) {
        document[_0x45d0[11]](galList[_0x344ax4])[_0x45d0[10]][_0x45d0[9]] = _0x45d0[12] + window[_0x45d0[13]] / 10 + _0x45d0[14] + window[_0x45d0[13]] / 10 + _0x45d0[15]
    }
}

function selectTab(_0x344ax6, _0x344ax7) {
    var _0x344ax8 = document[_0x45d0[11]](_0x344ax6);
    var _0x344ax9 = document[_0x45d0[11]](_0x344ax7);
    _0x344ax8[_0x45d0[10]][_0x45d0[16]] = _0x45d0[17];
    _0x344ax9[_0x45d0[10]][_0x45d0[18]] = _0x45d0[19];
    var _0x344axa = galList[_0x45d0[20]]();
    var _0x344axb = _0x344axa[_0x45d0[21]](_0x344ax6);
    _0x344axa[_0x45d0[22]](_0x344axb, 1);
    var _0x344axc = tabList[_0x45d0[20]]();
    var _0x344axd = _0x344axc[_0x45d0[21]](_0x344ax7);
    _0x344axc[_0x45d0[22]](_0x344axd, 1);
    for (var _0x344ax4 = 0; _0x344ax4 < _0x344axa[_0x45d0[8]]; _0x344ax4++) {
        var _0x344axe = document[_0x45d0[11]](_0x344axa[_0x344ax4]);
        var _0x344axf = document[_0x45d0[11]](_0x344axc[_0x344ax4]);
        _0x344axe[_0x45d0[10]][_0x45d0[16]] = _0x45d0[23];
        _0x344axf[_0x45d0[10]][_0x45d0[18]] = _0x45d0[17]
    }
}

function autoResizeDiv() {
    document[_0x45d0[11]](_0x45d0[25])[_0x45d0[10]][_0x45d0[24]] = window[_0x45d0[26]] + _0x45d0[15]
}
//window[_0x45d0[27]] = setGalWidth;
//autoResizeDiv()