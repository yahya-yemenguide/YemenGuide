USE DalilakAlYamaniDB;
GO

/*
    إعدادات إشعارات الملف الشخصي للمالك والعميل.
    السكربت آمن للتشغيل أكثر من مرة؛ لا يضيف العمود إذا كان موجوداً مسبقاً.
*/

IF COL_LENGTH('dbo.Users', 'NewReservationAlert') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD NewReservationAlert BIT NOT NULL
        CONSTRAINT DF_Users_NewReservationAlert DEFAULT ((0)) WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Users', 'NewMessageAlert') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD NewMessageAlert BIT NOT NULL
        CONSTRAINT DF_Users_NewMessageAlert DEFAULT ((0)) WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Users', 'NewReviewAlert') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD NewReviewAlert BIT NOT NULL
        CONSTRAINT DF_Users_NewReviewAlert DEFAULT ((0)) WITH VALUES;
END;
GO

IF COL_LENGTH('dbo.Users', 'SoundAlert') IS NULL
BEGIN
    ALTER TABLE dbo.Users
    ADD SoundAlert BIT NOT NULL
        CONSTRAINT DF_Users_SoundAlert DEFAULT ((0)) WITH VALUES;
END;
GO
