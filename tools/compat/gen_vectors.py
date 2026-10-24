"""生成密码学黄金向量（开发期取证工具，不参与发布）。

用途：为 `tests/EasiLoginer.Infrastructure.Tests/SecurityTests.cs` 提供
"由 Python cryptography / argon2-cffi 真实生成"的密钥与令牌，用于验证
Fernet / Argon2id / 账号脱敏的逐字节兼容性（文档 §11.4）。

用法（在工作区根目录，依赖装在隔离 venv 里，不污染系统 Python）：

    python -m venv .cache/pyenv
    .cache/pyenv/Scripts/python -m pip install cryptography argon2-cffi
    .cache/pyenv/Scripts/python tools/compat/gen_vectors.py

输出为 JSON；改动 `SecurityTests` 中的常量时同步更新。
"""

import base64
import hashlib
import json

from argon2.low_level import Type, hash_secret_raw
from cryptography.fernet import Fernet

# 与 C# 测试中的 Password / SaltBase64 保持一致
PASSWORD = "Correct Horse Battery Staple 中文"
SALT = bytes(range(16))

# 与上游 core/security.py 的 encryption_check 负载一致（文档 §7.3）
MASTER_CHECK_PAYLOAD = b"EasiAuto-profile-master-check"

# 与上游 ARGON2_* 参数一致
ARGON2_MEMORY_COST = 65536
ARGON2_ITERATIONS = 3
ARGON2_LANES = 4


def derive(password: str, salt: bytes) -> bytes:
    """按上游参数派生 Fernet 密钥（URL-safe Base64，保留 '=' 填充）。"""
    raw = hash_secret_raw(
        secret=password.encode("utf-8"),
        salt=salt,
        time_cost=ARGON2_ITERATIONS,
        memory_cost=ARGON2_MEMORY_COST,
        parallelism=ARGON2_LANES,
        hash_len=32,
        type=Type.ID,
    )
    return base64.urlsafe_b64encode(raw)


def main() -> None:
    key = derive(PASSWORD, SALT)
    fernet = Fernet(key)

    vectors = {
        "password": PASSWORD,
        "password_utf8_base64": base64.b64encode(PASSWORD.encode("utf-8")).decode("ascii"),
        "salt_base64": base64.b64encode(SALT).decode("ascii"),
        "fernet_key": key.decode("ascii"),
        "token_master_check": fernet.encrypt(MASTER_CHECK_PAYLOAD).decode("ascii"),
        "token_utf8": fernet.encrypt("档案密码：p@ssw0rd 中文".encode("utf-8")).decode("ascii"),
        "account_hash": hashlib.sha256("student@example.com".encode("utf-8")).hexdigest()[:16],
    }

    print(json.dumps(vectors, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
