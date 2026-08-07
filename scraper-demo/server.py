# -*- coding: utf-8 -*-
"""
本地演示站点：用于对比「MV3 版（只能爬同域）」与「你的浏览器（借宿主桥爬跨域）」两种抓法。

启动后会有两个 origin：
  - 同域站点 : http://127.0.0.1:8090/   （商品列表，3 页，互相用链接串联）
  - 跨域端点 : http://127.0.0.1:8091/   （另一个 origin 的页面，模拟第三方站点）

用法：
  1) 运行：python server.py
  2) 在你的浏览器打开 http://127.0.0.1:8090/
  3) 打开网页爬虫插件：
     - MV3 版（Chrome/Edge 加载 browser-extensions/web-scraper）：整站爬取时只会抓到 :8090 的 3 页；
     - 你自己的浏览器 + plugins/web-scraper：勾选「跨域抓取」后，会顺带抓到 :8091 的跨域页面。
"""
import os
import socketserver
import threading
from http.server import SimpleHTTPRequestHandler

SAME_PORT = 8090
CROSS_PORT = 8091
ROOT = os.path.dirname(os.path.abspath(__file__))


class SameHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=os.path.join(ROOT, "site"), **kwargs)

    def log_message(self, *args):  # 静默访问日志
        pass


class CrossHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=os.path.join(ROOT, "cross"), **kwargs)

    def log_message(self, *args):
        pass


def serve(port, handler):
    with socketserver.ThreadingTCPServer(("127.0.0.1", port), handler) as httpd:
        httpd.serve_forever()


if __name__ == "__main__":
    t1 = threading.Thread(target=serve, args=(SAME_PORT, SameHandler), daemon=True)
    t2 = threading.Thread(target=serve, args=(CROSS_PORT, CrossHandler), daemon=True)
    t1.start()
    t2.start()
    print(f"同域站点 : http://127.0.0.1:{SAME_PORT}/")
    print(f"跨域端点 : http://127.0.0.1:{CROSS_PORT}/")
    print("按 Ctrl+C 退出")
    try:
        while True:
            threading.Event().wait(1)
    except KeyboardInterrupt:
        print("\n已停止演示站点")
