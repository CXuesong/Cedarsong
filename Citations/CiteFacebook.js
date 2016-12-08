// ==UserScript==
// @name         Cite Facebook!
// @namespace    http://cxuesong.com/
// @version      0.1
// @description  A script making citations from Facebook easier.
// @author       CXuesong
// @match        https://www.facebook.com/*
// @grant        GM_setClipboard
// @require      https://code.jquery.com/jquery-2.1.4.min.js
// ==/UserScript==

window.setTimeout((function() {
    'use strict';
    function GetDate(date)
    {
        var ds = date.toISOString();
        return ds.substring(0, ds.indexOf("T"));
    }
    function NormalizeDate(dateExpr)
    {
        if (!dateExpr) return null;
        dateExpr = dateExpr.trim();
        if (dateExpr.length > 0) return GetDate(new Date(dateExpr * 1000));
        return null;
    }
    function Cleanup(expr)
    {
        return expr.replace(/\s+/, " ").trim();
    }
    function CommentParser(commentBody)
    {
        return {
            author : $("a.UFICommentActorName", commentBody).text().trim(),
            date : NormalizeDate($(".livetimestamp", commentBody).attr("data-utime")),
            link : "https://www.facebook.com" + $("a.uiLinkSubtle", commentBody).attr("href"),
        };
    }
    function StatusParser()
    {
        /*return {
            title : Cleanup($(".entry-title").text()),
            author : Cleanup($("article .author").text()),
            date :  NormalizeDate($("article time").text()),
            anchor : null,
        };*/
    }
    var now = GetDate(new Date());
    ////////// Comments //////////
    // Decide the title
    var title = Cleanup(document.title);
    $(".UFIComment").each(function (index) {
        var data = CommentParser(this);
        var citeButton = $('<a>引用评论</a>');
        var commentUrl = data.link;
        citeButton.click(function (e) {
            GM_setClipboard($(this).attr("refdata"), "text");
        });
        citeButton.attr("href", "#");
        citeButton.attr("refdata", "<ref>{{Cite web |url=" + commentUrl + " |title=" + title + " |accessdate=" + now + " |author=" + data.author + " |work=Facebook" + " |date=" + (data.date || "") + " |quote= }}</ref>");
        var panel = $(".UFICommentActions", this);
        console.log(panel);
        if (panel) panel.append(citeButton);
        else $(".UFICommentBody", this).after(citeButton);
    });
}), 5000);