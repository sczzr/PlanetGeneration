#!/usr/bin/env python3
import http.server
import socketserver
import mimetypes

# 添加 JavaScript MIME 类型
mimetypes.add_type('application/javascript', '.js')
mimetypes.add_type('application/javascript', '.min.js')

PORT = 8000

class CustomHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Access-Control-Allow-Origin', '*')
        super().end_headers()
    
    def guess_type(self, path):
        if path.endswith('.js') or path.endswith('.min.js'):
            return 'application/javascript'
        return super().guess_type(path)

if __name__ == '__main__':
    with socketserver.TCPServer(("", PORT), CustomHandler) as httpd:
        print(f"Server running at http://localhost:{PORT}/")
        httpd.serve_forever()