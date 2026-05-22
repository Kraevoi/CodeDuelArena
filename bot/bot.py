import os
import sys
import asyncio
import re
from datetime import datetime

import psycopg2
from psycopg2.extras import RealDictCursor
from dotenv import load_dotenv
from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup
from telegram.ext import Application, CommandHandler, MessageHandler, CallbackQueryHandler, filters, ContextTypes

load_dotenv()

BOT_TOKEN = os.getenv("BOT_TOKEN")
SUPPORT_CHAT_ID = os.getenv("SUPPORT_CHAT_ID")
DATABASE_URL = os.getenv("DATABASE_URL")

# Состояния пользователей
user_states = {}  # chat_id -> state
support_sessions = {}  # user_chat_id -> admin_chat_id

# ==================== БАЗА ДАННЫХ ====================

def get_db():
    return psycopg2.connect(DATABASE_URL, sslmode="require", cursor_factory=RealDictCursor)

def get_user_stats(username: str) -> dict | None:
    try:
        with get_db() as conn:
            with conn.cursor() as cur:
                cur.execute('SELECT "Username", "Score", "Wins", "Losses", "RegisteredAt" FROM "Users" WHERE "Username" = %s', (username,))
                return cur.fetchone()
    except:
        return None

def get_user_by_telegram(chat_id: str) -> dict | None:
    try:
        with get_db() as conn:
            with conn.cursor() as cur:
                cur.execute('SELECT "Username" FROM "UserSettings" WHERE "TelegramChatId" = %s', (chat_id,))
                row = cur.fetchone()
                if row:
                    return get_user_stats(row["Username"])
    except:
        pass
    return None

def link_telegram(chat_id: str, username: str) -> bool:
    try:
        with get_db() as conn:
            with conn.cursor() as cur:
                cur.execute('SELECT "Username" FROM "Users" WHERE "Username" = %s', (username,))
                if not cur.fetchone():
                    return False
                
                cur.execute('SELECT "Username" FROM "UserSettings" WHERE "Username" = %s', (username,))
                if cur.fetchone():
                    cur.execute('UPDATE "UserSettings" SET "TelegramChatId" = %s WHERE "Username" = %s', (chat_id, username))
                else:
                    cur.execute('INSERT INTO "UserSettings" ("Username", "TelegramChatId") VALUES (%s, %s)', (username, chat_id))
                conn.commit()
                return True
    except Exception as e:
        print(f"Link error: {e}")
        return False

# ==================== КЛАВИАТУРЫ ====================

def main_menu_kb():
    return InlineKeyboardMarkup([
        [InlineKeyboardButton("📊 Статистика", callback_data="stats"),
         InlineKeyboardButton("🆘 Поддержка", callback_data="support")],
        [InlineKeyboardButton("🔗 Привязать аккаунт", callback_data="link"),
         InlineKeyboardButton("🏆 Турниры", callback_data="tournaments")]
    ])

# ==================== КОМАНДЫ ====================

async def start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    user = update.effective_user
    name = user.first_name or user.username or "User"
    text = (
        f"⚔️ *CodeDuel Arena Bot*\n\n"
        f"Добро пожаловать, {escape_md(name)}!\n\n"
        f"/stats — твоя статистика\n"
        f"/support — написать в поддержку\n"
        f"/link — привязать игровой аккаунт\n"
        f"/tournaments — информация о турнирах"
    )
    await update.message.reply_text(text, parse_mode="Markdown", reply_markup=main_menu_kb())

async def menu(update: Update, context: ContextTypes.DEFAULT_TYPE):
    await update.message.reply_text("⚔️ *Главное меню*", parse_mode="Markdown", reply_markup=main_menu_kb())

async def stats(update: Update, context: ContextTypes.DEFAULT_TYPE):
    chat_id = str(update.effective_chat.id)
    username = update.effective_user.username or update.effective_user.first_name or "User"
    
    # Ищем по привязанному аккаунту
    user = get_user_by_telegram(chat_id)
    if not user:
        # Ищем по username
        user = get_user_stats(username)
    
    if not user:
        await update.message.reply_text("⚠️ Аккаунт не найден. Привяжи аккаунт: /link")
        return
    
    wr = round(user["Wins"] / (user["Wins"] + user["Losses"]) * 100, 1) if (user["Wins"] + user["Losses"]) > 0 else 0
    
    text = (
        f"📊 *{escape_md(user['Username'])}*\n\n"
        f"⭐ Очки: *{user['Score']:,}*\n"
        f"🏆 Побед: *{user['Wins']}*  |  💀 Поражений: *{user['Losses']}*\n"
        f"📈 Винрейт: *{wr}%*\n"
        f"📅 Регистрация: {user['RegisteredAt'].strftime('%d.%m.%Y') if user['RegisteredAt'] else '—'}"
    )
    await update.message.reply_text(text, parse_mode="Markdown")

async def support_start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    chat_id = update.effective_chat.id
    user_states[chat_id] = "waiting_support"
    await update.message.reply_text("🆘 Опиши свою проблему одним сообщением.\n/stop — отмена.")

async def tournaments(update: Update, context: ContextTypes.DEFAULT_TYPE):
    text = (
        "🏆 *Турниры*\n\n"
        "🔥 Еженедельный кубок — каждую субботу 18:00 UTC\n"
        "🏅 Месячный чемпионат — первое воскресенье месяца\n\n"
        "Будь онлайн и вставай в очередь дуэлей!"
    )
    await update.message.reply_text(text, parse_mode="Markdown")

async def link_start(update: Update, context: ContextTypes.DEFAULT_TYPE):
    chat_id = update.effective_chat.id
    user_states[chat_id] = "waiting_link"
    await update.message.reply_text("Введи свой никнейм на CodeDuel Arena:")

async def stop(update: Update, context: ContextTypes.DEFAULT_TYPE):
    chat_id = update.effective_chat.id
    user_states.pop(chat_id, None)
    await update.message.reply_text("Сессия завершена. /menu — главное меню.")

# ==================== ОБРАБОТКА ТЕКСТА ====================

async def handle_message(update: Update, context: ContextTypes.DEFAULT_TYPE):
    chat_id = update.effective_chat.id
    text = update.message.text or ""
    username = update.effective_user.username or update.effective_user.first_name or "User"
    
    # Сообщение из группы поддержки (ответ админа)
    if str(chat_id) == SUPPORT_CHAT_ID or str(chat_id) == SUPPORT_CHAT_ID.replace("-", ""):
        if update.message.reply_to_message:
            reply_text = update.message.reply_to_message.text or ""
            match = re.search(r'#ID(\d+)', reply_text)
            if match:
                target_id = match.group(1)
                admin_reply = text
                admin_name = update.effective_user.first_name or "Support"
                try:
                    await context.bot.send_message(
                        target_id,
                        f"🛡️ *Ответ поддержки:*\n\n{escape_md(admin_reply)}\n\n_— {escape_md(admin_name)}_",
                        parse_mode="Markdown"
                    )
                except:
                    pass
        return
    
    # Проверка состояний
    state = user_states.pop(chat_id, None)
    
    if state == "waiting_support":
        if SUPPORT_CHAT_ID and SUPPORT_CHAT_ID != "YOUR_CHAT_ID":
            support_msg = (
                f"🆘 *Запрос в поддержку* #ID{chat_id}\n\n"
                f"👤 {escape_md(username)} (ID: `{chat_id}`)\n"
                f"📝 {escape_md(text)}\n\n"
                f"_Ответьте на это сообщение, чтобы ответить пользователю_"
            )
            try:
                await context.bot.send_message(SUPPORT_CHAT_ID, support_msg, parse_mode="Markdown")
                await update.message.reply_text("✅ Отправлено в поддержку. Ответ придёт сюда.")
            except Exception as e:
                await update.message.reply_text(f"⚠️ Ошибка отправки: {e}")
        else:
            await update.message.reply_text("⚠️ Поддержка временно недоступна.")
        return
    
    if state == "waiting_link":
        if link_telegram(str(chat_id), text):
            await update.message.reply_text(f"✅ Аккаунт *{escape_md(text)}* привязан!", parse_mode="Markdown")
        else:
            await update.message.reply_text("❌ Пользователь не найден. Зарегистрируйся на codeduelarena.onrender.com")
        return
    
    # Обычное сообщение
    await update.message.reply_text("Используй /menu или кнопки ниже.", reply_markup=main_menu_kb())

# ==================== CALLBACK (КНОПКИ) ====================

async def handle_callback(update: Update, context: ContextTypes.DEFAULT_TYPE):
    query = update.callback_query
    await query.answer()
    
    data = query.data
    chat_id = query.message.chat.id
    user = query.from_user
    name = user.first_name or user.username or "User"
    
    if data == "stats":
        user_data = get_user_by_telegram(str(chat_id)) or get_user_stats(user.username or name)
        if user_data:
            wr = round(user_data["Wins"] / (user_data["Wins"] + user_data["Losses"]) * 100, 1) if (user_data["Wins"] + user_data["Losses"]) > 0 else 0
            text = (
                f"📊 *{escape_md(user_data['Username'])}*\n\n"
                f"⭐ Очки: *{user_data['Score']:,}*\n"
                f"🏆 Побед: *{user_data['Wins']}*  |  💀 Поражений: *{user_data['Losses']}*\n"
                f"📈 Винрейт: *{wr}%*"
            )
        else:
            text = "⚠️ Аккаунт не найден. /link"
        await query.edit_message_text(text, parse_mode="Markdown", reply_markup=main_menu_kb())
    
    elif data == "support":
        user_states[chat_id] = "waiting_support"
        await query.edit_message_text("🆘 Опиши свою проблему одним сообщением.\n/stop — отмена.")
    
    elif data == "link":
        user_states[chat_id] = "waiting_link"
        await query.edit_message_text("Введи свой никнейм на CodeDuel Arena:")
    
    elif data == "tournaments":
        text = "🏆 *Турниры*\n\nЕженедельный кубок — каждую субботу 18:00 UTC.\nМесячный чемпионат — первое воскресенье месяца."
        await query.edit_message_text(text, parse_mode="Markdown", reply_markup=main_menu_kb())

# ==================== УТИЛИТЫ ====================

def escape_md(text: str) -> str:
    for char in "_*[]()~`>#+-=|{}.!":
        text = text.replace(char, f"\\{char}")
    return text

# ==================== ЗАПУСК ====================

def main():
    app = Application.builder().token(BOT_TOKEN).build()
    
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("menu", menu))
    app.add_handler(CommandHandler("stats", stats))
    app.add_handler(CommandHandler("support", support_start))
    app.add_handler(CommandHandler("link", link_start))
    app.add_handler(CommandHandler("tournaments", tournaments))
    app.add_handler(CommandHandler("stop", stop))
    app.add_handler(CallbackQueryHandler(handle_callback))
    app.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, handle_message))
    
    print("=== BOT STARTED ===")
    app.run_polling(allowed_updates=Update.ALL_TYPES)

if __name__ == "__main__":
    main()